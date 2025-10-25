namespace MahoBootstrap.Outputs;

public class PrinterOutput : Output
{
    public override void Accept(string targetFolder)
    {
        foreach (var proto in Program.models.Values)
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