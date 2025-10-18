using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
using com.github.javaparser.ast.expr;
using com.github.javaparser.ast.stmt;
using com.github.javaparser.ast.type;
using MahoBootstrap.Models;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Outputs;

public abstract class JavaOutputBase : IOutput
{
    public void Accept(string targetFolder, FrozenDictionary<string, ClassModel> models)
    {
        var binPath = Path.Combine(targetFolder, "bin");
        var sourcePath = Path.Combine(targetFolder, "source");
        if (Directory.Exists(sourcePath))
            Directory.Delete(sourcePath, true);
        foreach (var model in models.Values)
        {
            CompilationUnit cu = new CompilationUnit();
            cu.setPackageDeclaration(model.pkg);

            List<Modifier.Keyword> classMods = new()
            {
                Modifier.Keyword.PUBLIC
            };
            switch (model.classType)
            {
                case ClassType.Final:
                    classMods.Add(Modifier.Keyword.FINAL);
                    break;
                case ClassType.Abstract:
                    classMods.Add(Modifier.Keyword.ABSTRACT);
                    break;
            }

            ClassOrInterfaceDeclaration cls = model.classType == ClassType.Interface
                ? cu.addInterface(model.name, classMods.ToArray())
                : cu.addClass(model.name, classMods.ToArray());

            foreach (var field in model.fields)
            {
                if (field.type == (MemberType.Final | MemberType.Static))
                {
                    var stubName = "_stubFor_" + field.name;
                    var stubGetter = cls.addMethod(stubName,
                    [
                        Modifier.Keyword.PRIVATE, Modifier.Keyword.STATIC, Modifier.Keyword.NATIVE
                    ]);
                    stubGetter.setType(field.fieldType);
                    stubGetter.removeBody();
                    var init = StaticJavaParser.parseExpression($"{stubName}()");
                    cls.addMember(ToInitedField(init, field));
                }
                else
                {
                    cls.addField(field.fieldType, field.name, ToKeywords(field.access, field.type));
                }
            }

            foreach (var field in model.consts)
            {
                var init = StaticJavaParser.parseExpression(field.constantValue ?? "null");
                cls.addMember(ToInitedField(init, field));
            }


            if (model.classType == ClassType.Interface)
            {
                foreach (var implements in model.implements)
                    cls.addExtends(ResolveName(implements, models));
                foreach (var method in model.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, MemberType.Abstract));
                    m.setType(ResolveName(method.returnType, models));

                    foreach (var arg in method.arguments)
                        m.addParameter(ResolveName(arg.type, models), arg.name);

                    foreach (var @throw in method.throws)
                        m.addThrownException(StaticJavaParser.parseType(@throw) as ReferenceType);

                    m.removeBody();
                }
            }
            else
            {
                if (model.name != "java.lang.Object")
                {
                    if (model.parent != null)
                        cls.addExtends(ResolveName(model.parent, models));
                    foreach (var implements in model.implements)
                        cls.addImplements(ResolveName(implements, models));
                }

                if (model.ctors.Length == 0)
                {
                    cls.addConstructor(ToKeywords(MemberAccess.Package, MemberType.Regular));
                }

                foreach (var ctor in model.ctors)
                {
                    var c = cls.addConstructor(ToKeywords(ctor.access, MemberType.Regular));
                    foreach (var arg in ctor.arguments)
                        c.addParameter(arg.type, arg.name);
                    foreach (var @throw in ctor.throws)
                        c.addThrownException(StaticJavaParser.parseType(@throw) as ReferenceType);
                    if (model.parent != null && models.TryGetValue(model.parent, out var parent))
                    {
                        bool needCtorCall = parent.ctors.Length != 0 && !parent.ctors.Any(x => x.arguments.Length == 0);
                        if (needCtorCall)
                        {
                            var ctorCode = c.createBody();
                            if (parent.ctors.Any(x => x.HasSameSignature(ctor)))
                            {
                                ctorCode.addStatement(ToParentCtorCall("super", ctor.arguments));
                            }
                            else
                            {
                                bool nothingFound = false;
                                if (ctor.arguments.Length == 0)
                                {
                                    nothingFound = true;
                                }
                                else
                                {
                                    var args = ctor.arguments.ToList();

                                    while (true)
                                    {
                                        args.RemoveAt(args.Count - 1);
                                        if (args.Count == 0)
                                        {
                                            nothingFound = true;
                                            break;
                                        }

                                        if (model.ctors.Any(x => x.HasSameSignature(args)))
                                        {
                                            ctorCode.addStatement(ToParentCtorCall("this", args));
                                            break;
                                        }

                                        if (parent.ctors.Any(x => x.HasSameSignature(args)))
                                        {
                                            ctorCode.addStatement(ToParentCtorCall("super", args));
                                            break;
                                        }
                                    }
                                }

                                if (nothingFound)
                                {
                                    StringBuilder sb = new("super");
                                    sb.Append('(');
                                    for (var i = 0; i < parent.ctors[0].arguments.Length; i++)
                                    {
                                        var arg = parent.ctors[0].arguments[i];
                                        if (i != 0)
                                            sb.Append(',');
                                        sb.Append(ConstModel.GetDefaultValue(arg.type, true));
                                    }

                                    sb.Append(");");
                                    ctorCode.addStatement(StaticJavaParser.parseStatement(sb.ToString()));
                                }
                            }
                        }
                    }
                }

                foreach (var method in model.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, method.type));
                    m.setType(ResolveName(method.returnType, models));

                    foreach (var arg in method.arguments)
                        m.addParameter(ResolveName(arg.type, models), arg.name);

                    foreach (var @throw in method.throws)
                        m.addThrownException(StaticJavaParser.parseType(@throw) as ReferenceType);

                    if (m.isAbstract())
                        m.removeBody();
                    else
                        FillMethodBody(m, method, model);
                }
            }

            string basePath = Path.Combine(sourcePath, Path.Combine(model.pkg.Split('.')));
            Directory.CreateDirectory(basePath);

            var filePath = Path.Combine(basePath, model.name + ".java");
            File.WriteAllText(filePath, cu.toString());
        }

        Directory.CreateDirectory(binPath);
        ProcessStartInfo psi = new ProcessStartInfo("/usr/bin/bash");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(
            $"javac -d \"{binPath}\" -sourcepath \"{sourcePath}\" -bootclasspath classes `find \"{sourcePath}\" -name \"*.java\"`");
        Process.Start(psi).WaitForExit();
    }

    private static Modifier.Keyword[] ToKeywords(MemberAccess memberAccess, MemberType fieldMemberType)
    {
        List<Modifier.Keyword> mods = new();
        ToKeywords(memberAccess, mods);
        ToKeywords(fieldMemberType, mods);
        var keywords = mods.ToArray();
        return keywords;
    }

    protected abstract void FillMethodBody(MethodDeclaration m, MethodModel model, ClassModel cls);

    private static Statement ToParentCtorCall(string name, IList<CodeArgument> args)
    {
        StringBuilder sb = new(name);
        sb.Append('(');

        for (var i = 0; i < args.Count; i++)
        {
            if (i != 0)
                sb.Append(',');
            sb.Append(args[i].name);
        }

        sb.Append(");");

        return StaticJavaParser.parseStatement(sb.ToString());
    }

    private static FieldDeclaration ToInitedField(Expression init, DataModel model)
    {
        var type = StaticJavaParser.parseType(model.fieldType);
        var decl = new VariableDeclarator(type, model.name, init);
        return new FieldDeclaration(AsNodeList(ToMods(model.access, model.type)), decl);
    }

    public static void ToKeywords(MemberAccess ma, List<Modifier.Keyword> mods)
    {
        switch (ma)
        {
            case MemberAccess.Public:
                mods.Add(Modifier.Keyword.PUBLIC);
                break;
            case MemberAccess.Private:
                mods.Add(Modifier.Keyword.PRIVATE);
                break;
            case MemberAccess.Protected:
                mods.Add(Modifier.Keyword.PROTECTED);
                break;
        }
    }

    public static Modifier[] ToMods(MemberAccess ma, MemberType mt) =>
        ToKeywords(ma, mt).Select(x => new Modifier(x)).ToArray();

    public static NodeList AsNodeList<T>(IEnumerable<T> list) where T : Node => new(list.Cast<Node>().ToArray());

    public static void ToKeywords(MemberType mt, List<Modifier.Keyword> mods)
    {
        if (mt.HasFlag(MemberType.Abstract))
            mods.Add(Modifier.Keyword.ABSTRACT);
        if (mt.HasFlag(MemberType.Final))
            mods.Add(Modifier.Keyword.FINAL);
        if (mt.HasFlag(MemberType.Static))
            mods.Add(Modifier.Keyword.STATIC);
    }

    private static string ResolveName(string name, FrozenDictionary<string, ClassModel> classes)
    {
        if (name.Contains('.'))
            return name;
        var candidates = classes.Keys.Where(x => x.EndsWith($".{name}")).ToList();
        if (candidates.Count == 0)
            return name;
        if (candidates.Count == 1)
            return candidates[0];
        throw new ArgumentException($"Multiple classes with name \"{name}\" found");
    }
}