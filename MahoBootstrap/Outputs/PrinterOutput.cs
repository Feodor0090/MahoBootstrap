using System.Collections.Frozen;
using MahoBootstrap.Models;

namespace MahoBootstrap.Outputs;

public class PrinterOutput : IOutput
{
    public void Accept(string targetFolder, FrozenDictionary<string, ClassModel> models)
    {
        foreach (var proto in models.Values)
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