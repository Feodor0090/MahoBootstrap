using System.Collections.Frozen;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
using com.github.javaparser.ast.expr;
using MahoBootstrap.Models;

namespace MahoBootstrap.Outputs;

public class JavaHeadersOutput : JavaOutputBase
{
    public JavaHeadersOutput(FrozenDictionary<string, ClassModel> models) : base()
    {
    }

    protected override void FillMethodBody(MethodDeclaration m, MethodModel proto, ClassModel cls)
    {
        m.removeBody();
        if (!m.isAbstract())
            m.setNative(true);
    }

    protected override Expression GetReadonlyInitializer(FieldModel field, ClassOrInterfaceDeclaration cls)
    {
        var stubName = "_stubFor_" + field.name;
        var stubGetter = cls.addMethod(stubName,
        [
            Modifier.Keyword.PRIVATE, Modifier.Keyword.STATIC, Modifier.Keyword.NATIVE
        ]);
        stubGetter.setType(field.fieldType);
        stubGetter.removeBody();
        var init = StaticJavaParser.parseExpression($"{stubName}()");
        return init;
    }
}