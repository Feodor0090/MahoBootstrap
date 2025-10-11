using System.Collections.Frozen;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
using com.github.javaparser.ast.type;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Outputs;

public abstract class JavaOutputBase : IOutput
{
    public void Accept(string targetFolder, FrozenDictionary<string, ClassPrototype> prototypes)
    {
        foreach (var proto in prototypes.Values)
        {
            CompilationUnit cu = new CompilationUnit();
            cu.setPackageDeclaration(proto.pkg);

            List<Modifier.Keyword> classMods = new()
            {
                Modifier.Keyword.PUBLIC
            };
            switch (proto.type)
            {
                case ClassType.Final:
                    classMods.Add(Modifier.Keyword.FINAL);
                    break;
                case ClassType.Abstract:
                    classMods.Add(Modifier.Keyword.ABSTRACT);
                    break;
            }

            ClassOrInterfaceDeclaration cls = proto.type == ClassType.Interface
                ? cu.addInterface(proto.name, classMods.ToArray())
                : cu.addClass(proto.name, classMods.ToArray());

            foreach (var field in proto.fields)
            {
                if (field.value == null && !StaticJavaParser.parseType(field.fieldType).isReferenceType())
                    cls.addField(field.fieldType, field.name, ToKeywords(field.access, field.memberType));
                else
                {
                    var init = StaticJavaParser.parseExpression(field.value ?? "null");
                    var type = StaticJavaParser.parseType(field.fieldType);
                    var decl = new VariableDeclarator(type, field.name, init);
                    var nodeList = new NodeList(ToKeywords(field.access, field.memberType)
                        .Select(x => (Node)new Modifier(x)).ToArray());
                    var fd2 = new FieldDeclaration(nodeList, decl);
                    cls.addMember(fd2);
                }
            }

            if (proto.type == ClassType.Interface)
            {
                foreach (var implements in proto.implements)
                    cls.addExtends(implements);
                foreach (var method in proto.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, MemberType.Abstract));
                    m.setType(method.returnType);

                    foreach (var arg in method.args)
                        m.addParameter(arg.type, arg.name);

                    foreach (var @throw in method.throws)
                        m.addThrownException(StaticJavaParser.parseType(@throw) as ReferenceType);

                    m.removeBody();
                }
            }
            else
            {
                if (proto.name != "java.lang.Object")
                {
                    if (proto.parent != null)
                        cls.addExtends(proto.parent);
                    foreach (var implements in proto.implements)
                        cls.addImplements(implements);
                }

                if (proto.constructors.Count == 0)
                {
                    cls.addConstructor(ToKeywords(MemberAccess.Package, MemberType.Regular));
                }

                foreach (var ctor in proto.constructors)
                {
                    var c = cls.addConstructor(ToKeywords(ctor.access, MemberType.Regular));
                    foreach (var arg in ctor.args)
                        c.addParameter(arg.type, arg.name);
                    foreach (var @throw in ctor.throws)
                        c.addThrownException(new ClassOrInterfaceType(@throw));
                }

                foreach (var method in proto.methods)
                {
                    var m = cls.addMethod(method.name, ToKeywords(method.access, method.type));
                    m.setType(method.returnType);

                    foreach (var arg in method.args)
                        m.addParameter(arg.type, arg.name);

                    foreach (var @throw in method.throws)
                        m.addThrownException(new ClassOrInterfaceType(@throw));

                    if (m.isAbstract())
                        m.removeBody();
                    else
                        FillMethodBody(m, method, proto);
                }
            }

            string basePath = Path.Combine(targetFolder, Path.Combine(proto.pkg.Split('.')));
            Directory.CreateDirectory(basePath);

            var filePath = Path.Combine(basePath, proto.name + ".java");
            File.Delete(filePath);
            File.WriteAllText(filePath, cu.toString());
        }
    }

    private static Modifier.Keyword[] ToKeywords(MemberAccess memberAccess, MemberType fieldMemberType)
    {
        List<Modifier.Keyword> mods = new();
        ToKeywords(memberAccess, mods);
        ToKeywords(fieldMemberType, mods);
        var keywords = mods.ToArray();
        return keywords;
    }

    protected abstract void FillMethodBody(MethodDeclaration m, MethodPrototype proto, ClassPrototype cls);

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