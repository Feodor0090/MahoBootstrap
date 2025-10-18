using System.Collections.Frozen;
using MahoBootstrap;
using MahoBootstrap.Models;
using MahoBootstrap.Outputs;

// ARGS
string[] docRoots =
[
    "/home/ansel/repos/j2me/J2ME_Docs/docs/midp-2.0",
    "/home/ansel/repos/j2me/J2ME_Docs/docs/cldc-1.1",
    "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr135",
    "/home/ansel/Desktop/javadocs/jsr184"
];
string target = "nativejava";

Dictionary<string, ClassModel> classes = new();

foreach (var docRoot in docRoots)
{
    var constsPath = Path.Combine(docRoot, "constant-values.html");
    FrozenDictionary<string, FrozenDictionary<string, string>> consts;
    if (File.Exists(constsPath))
        consts = JavaDocReader.ExtractConstants(File.ReadAllText(constsPath));
    else
        consts = FrozenDictionary<string, FrozenDictionary<string, string>>.Empty;

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
            if (file.Contains("/copyright-notice.html"))
                continue;
            if (file.Contains("/copyright.html"))
                continue;
            if (file.Contains("/index-files/"))
                continue;
            try
            {
                var text = File.ReadAllText(file);
                var proto = JavaDocReader.Parse(text);
                var modelNext = new ClassModel(proto, file);
                if (consts.TryGetValue(modelNext.fullName, out var map))
                    JavaDocReader.ApplyConstants(modelNext, map);

                if (!classes.TryAdd(proto.fullName, modelNext))
                {
                    var modelPrev = classes[proto.fullName];
                    classes[proto.fullName] = new ClassModel(modelPrev, modelNext);
                }
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
}


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
    new T().Accept("/tmp/mbs", classes.ToFrozenDictionary());
}