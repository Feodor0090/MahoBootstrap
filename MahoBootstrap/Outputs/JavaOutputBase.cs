using System.Diagnostics;
using System.Text;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
using com.github.javaparser.ast.expr;
using com.github.javaparser.ast.nodeTypes;
using com.github.javaparser.ast.stmt;
using com.github.javaparser.ast.type;
using MahoBootstrap.Models;
using MahoBootstrap.Prototypes;
using static com.github.javaparser.ast.Modifier.Keyword;
using static com.github.javaparser.StaticJavaParser;

namespace MahoBootstrap.Outputs;

public abstract class JavaOutputBase : Output
{
    public override void Accept(string targetFolder)
    {
        var binPath = Path.Combine(targetFolder, "bin");
        var sourcePath = Path.Combine(targetFolder, "source");
        if (Directory.Exists(sourcePath))
            Directory.Delete(sourcePath, true);
        foreach (var model in Program.models.Values)
        {
            var cu = new CompilationUnit();
            cu.setPackageDeclaration(model.pkg);

            ClassOrInterfaceDeclaration cls = model.classType switch
            {
                ClassType.Regular => cu.addClass(model.name, [PUBLIC]),
                ClassType.Final => cu.addClass(model.name, [PUBLIC, FINAL]),
                ClassType.Abstract => cu.addClass(model.name, [PUBLIC, ABSTRACT]),
                ClassType.Interface => cu.addInterface(model.name, [PUBLIC]),
                _ => throw new ArgumentOutOfRangeException()
            };

            foreach (var field in model.fields)
            {
                if (field.type == (MemberType.Final | MemberType.Static))
                    cls.addMember(ToInitedField(GetReadonlyInitializer(field, cls), field));
                else
                    cls.addField(field.fieldType, field.name, ToKeywords(field.access, field.type));
            }

            foreach (var field in model.consts)
                cls.addMember(ToInitedField(parseExpression(field.constantValue ?? "null"), field));

            foreach (var method in model.methods)
            {
                var m = cls.addMethod(method.name,
                    ToKeywords(method.access, model.isInterface ? MemberType.Abstract : method.type));
                m.setType(ResolveName(method.returnType));

                SetArgs(m, method);
                SetThrows(m, method);

                if (m.isAbstract())
                    m.removeBody();
                else
                    FillMethodBody(m, method, model);
            }

            if (model.isInterface)
            {
                foreach (var implements in model.implements)
                    cls.addExtends(ResolveName(implements));
            }
            else
            {
                if (model.parent != null)
                    cls.addExtends(ResolveName(model.parent));
                foreach (var implements in model.implements)
                    cls.addImplements(ResolveName(implements));

                if (model.ctors.Length == 0)
                    cls.addConstructor(ToKeywords(MemberAccess.Package, MemberType.Regular));

                foreach (var ctor in model.ctors)
                {
                    var c = cls.addConstructor(ToKeywords(ctor.access, MemberType.Regular));
                    SetArgs(c, ctor);
                    SetThrows(c, ctor);
                    if (model.parent != null && Program.models.TryGetValue(model.parent, out var parent))
                    {
                        bool needCtorCall = parent.ctors.Length != 0 && !parent.ctors.Any(x => x.arguments.Length == 0);
                        if (needCtorCall)
                        {
                            var ctorCode = c.createBody();
                            if (parent.ctors.Any(x => x.HasSameSignature(ctor)))
                            {
                                ctorCode.addStatement(BuildCall("super", ctor.arguments));
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
                                            ctorCode.addStatement(BuildCall("this", args));
                                            break;
                                        }

                                        if (parent.ctors.Any(x => x.HasSameSignature(args)))
                                        {
                                            ctorCode.addStatement(BuildCall("super", args));
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
                                    ctorCode.addStatement(parseStatement(sb.ToString()));
                                }
                            }
                        }
                    }
                }
            }

            string basePath = Path.Combine(sourcePath, Path.Combine(model.pkg.Split('.')));
            Directory.CreateDirectory(basePath);

            var filePath = Path.Combine(basePath, $"{model.name}.java");
            File.WriteAllText(filePath, cu.toString());
        }

        Directory.CreateDirectory(binPath);
        ProcessStartInfo psi = new ProcessStartInfo("/usr/bin/bash");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(
            $"javac -d \"{binPath}\" -sourcepath \"{sourcePath}\" -bootclasspath classes `find \"{sourcePath}\" -name \"*.java\"`");
        Process.Start(psi)!.WaitForExit();
    }

    protected abstract Expression GetReadonlyInitializer(FieldModel field, ClassOrInterfaceDeclaration cls);

    protected abstract void FillMethodBody(MethodDeclaration m, MethodModel model, ClassModel cls);

    private static FieldDeclaration ToInitedField(Expression init, DataModel model)
    {
        var type = parseType(model.fieldType);
        var decl = new VariableDeclarator(type, model.name, init);
        return new FieldDeclaration(AsNodeList(ToMods(model.access, model.type)), decl);
    }

    public static string ResolveName(string name)
    {
        if (name.Contains('.'))
            return name;
        var candidates = Program.models.Keys.Where(x => x.EndsWith($".{name}")).ToList();
        if (candidates.Count == 0)
            return name;
        if (candidates.Count == 1)
            return candidates[0];
        throw new ArgumentException($"Multiple classes with name \"{name}\" found");
    }


    public static Modifier.Keyword[] ToKeywords(MemberAccess memberAccess, MemberType fieldMemberType)
    {
        List<Modifier.Keyword> mods = new();
        if (memberAccess == MemberAccess.Public)
            mods.Add(Modifier.Keyword.PUBLIC);
        else if (memberAccess == MemberAccess.Private)
            mods.Add(Modifier.Keyword.PRIVATE);
        else if (memberAccess == MemberAccess.Protected)
            mods.Add(Modifier.Keyword.PROTECTED);
        if (fieldMemberType.HasFlag(MemberType.Abstract))
            mods.Add(Modifier.Keyword.ABSTRACT);
        if (fieldMemberType.HasFlag(MemberType.Final))
            mods.Add(Modifier.Keyword.FINAL);
        if (fieldMemberType.HasFlag(MemberType.Static))
            mods.Add(Modifier.Keyword.STATIC);
        var keywords = mods.ToArray();
        return keywords;
    }

    private static Modifier[] ToMods(MemberAccess ma, MemberType mt) =>
        ToKeywords(ma, mt).Select(x => new Modifier(x)).ToArray();

    private static NodeList AsNodeList<T>(IEnumerable<T> list) where T : Node => new(list.Cast<Node>().ToArray());

    public void SetThrows(NodeWithThrownExceptions node, CodeModel model)
    {
        foreach (var modelThrow in model.throws)
            node.addThrownException(StaticJavaParser.parseType(ResolveName(modelThrow)) as ReferenceType);
    }

    public void SetArgs(NodeWithParameters node, CodeModel model)
    {
        foreach (var arg in model.arguments)
            node.addParameter(ResolveName(arg.type), arg.name);
    }

    private static Statement BuildCall(string methodName, IList<CodeArgument> args)
    {
        StringBuilder sb = new(methodName);
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
}