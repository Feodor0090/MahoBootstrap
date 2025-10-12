using System.Collections.Frozen;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Outputs;

public class PrinterOutput : IOutput
{
    public void Accept(string targetFolder, FrozenDictionary<string, ClassPrototype> prototypes)
    {
        foreach (var proto in prototypes.Values)
        {
            Console.WriteLine(proto.fullName + " extends " + proto.parent);
            foreach (var method in proto.methods)
            {
                Console.Write("   ");
                Console.WriteLine(method.returnType + " " + method.name + "()");
            }

            Console.WriteLine();
        }
    }
}