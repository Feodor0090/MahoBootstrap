using System.Collections.Frozen;
using MahoBootstrap.Models;

namespace MahoBootstrap.Outputs;

public class PrinterOutput : Output
{
    private readonly FrozenDictionary<string, ClassModel> models;

    public PrinterOutput(FrozenDictionary<string, ClassModel> models)
    {
        this.models = models;
    }

    public override void Accept(string targetFolder)
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