using System.Collections.Frozen;
using MahoBootstrap;
using MahoBootstrap.Outputs;
using MahoBootstrap.Prototypes;

// ARGS
string docRoot = "/home/ansel/Desktop/УБЕРЙОБАДОКИ";
string target = "nativejava";


Dictionary<string, ClassPrototype> protos = new();

foreach (var dir in Directory.EnumerateDirectories(docRoot))
{
    var name = Path.GetFileName(dir);
    if (name == "resources")
        continue;
    var files = Directory.EnumerateFiles(dir, "*.html", SearchOption.AllDirectories);
    foreach (var file in files)
    {
        if (file.Contains("/class-use/"))
            continue;
        if (file.Contains("doc-files/"))
            continue;
        if (file.Contains("/package-use.html"))
            continue;
        if (file.Contains("/package-tree.html"))
            continue;
        if (file.Contains("/package-frame.html"))
            continue;
        if (file.Contains("/package-summary.html"))
            continue;
        try
        {
            var text = File.ReadAllText(file);
            var proto = JavaDocReader.Parse(text);
            protos[proto.fullName] = proto;
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to parse " + file);
            Console.WriteLine(e);
            Console.WriteLine();
            Console.ResetColor();
        }
    }
}

JavaDocReader.ApplyConstants(protos, File.ReadAllText(Path.Combine(docRoot, "constant-values.html")));

switch (target)
{
    case "print":
        Use<PrinterOutput>();
        break;
    case "nativejava":
        Use<JavaHeadersOutput>();
        break;
}


void Use<T>() where T : IOutput, new()
{
    new T().Accept("/tmp/mbs/", protos.ToFrozenDictionary());
}
