using com.github.javaparser.ast.body;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Outputs;

public class JavaHeadersOutput : JavaOutputBase
{
    protected override void FillMethodBody(MethodDeclaration m, MethodPrototype proto, ClassPrototype cls)
    {
        m.removeBody();
        if (!m.isAbstract())
            m.setNative(true);
    }
}