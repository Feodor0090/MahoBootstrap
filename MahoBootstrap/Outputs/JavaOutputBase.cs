using System.Collections.Frozen;
using System.Diagnostics;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
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
                cls.addField(field.fieldType, field.name, ToKeywords(field.access, field.type));

            foreach (var field in model.consts)
            {
                var init = StaticJavaParser.parseExpression(field.constantValue ?? "null");
                var type = StaticJavaParser.parseType(field.fieldType);
                var decl = new VariableDeclarator(type, field.name, init);
                var nodeList = new NodeList(ToKeywords(field.access, field.type)
                    .Select(x => (Node)new Modifier(x)).ToArray());
                var fd2 = new FieldDeclaration(nodeList, decl);
                cls.addMember(fd2);
            }


            if (model.classType == ClassType.Interface)
            {
                foreach (var implements in model.implements)
                    cls.addExtends(implements);
                foreach (var method in model.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, MemberType.Abstract));
                    m.setType(method.returnType);

                    foreach (var arg in method.arguments)
                        m.addParameter(arg.type, arg.name);

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
                        cls.addExtends(model.parent);
                    foreach (var implements in model.implements)
                        cls.addImplements(implements);
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
                }

                foreach (var method in model.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, method.type));
                    m.setType(method.returnType);

                    foreach (var arg in method.arguments)
                        m.addParameter(arg.type, arg.name);

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

    public static void ToKeywords(MemberType mt, List<Modifier.Keyword> mods)
    {
        if (mt.HasFlag(MemberType.Abstract))
            mods.Add(Modifier.Keyword.ABSTRACT);
        if (mt.HasFlag(MemberType.Final))
            mods.Add(Modifier.Keyword.FINAL);
        if (mt.HasFlag(MemberType.Static))
            mods.Add(Modifier.Keyword.STATIC);
    }
}