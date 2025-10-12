using com.github.javaparser.ast.body;
using MahoBootstrap.Models;

namespace MahoBootstrap.Outputs;

public class JavaHeadersOutput : JavaOutputBase
{
    protected override void FillMethodBody(MethodDeclaration m, MethodModel proto, ClassModel cls)
    {
        m.removeBody();
        if (!m.isAbstract())
            m.setNative(true);
    }
}