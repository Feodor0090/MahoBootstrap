using System.Collections.Frozen;
using ikvm.extensions;
using MahoBootstrap.Models;
using MahoBootstrap.Outputs;

namespace MahoBootstrap;

internal class Program
{
    public static FrozenDictionary<string, ClassModel> models = null!;

    public static void Main(string[] args)
    {
        string[] docRoots =
        [
            "/home/ansel/repos/j2me/J2ME_Docs/docs/midp-2.0", // MIDP
            "/home/ansel/repos/j2me/J2ME_Docs/docs/cldc-1.1", // CLDC
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr135", // MMAPI
            "/home/ansel/Desktop/javadocs/jsr184", // M3G
            "/home/ansel/Desktop/javadocs/nokiaui", // NUI
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr211", // Content handler
            "/home/ansel/Desktop/javadocs/jsr75/file", // File system
            "/home/ansel/Desktop/javadocs/jsr75/pim", // Data system
            "/home/ansel/Desktop/javadocs/iapinfo", // AP info
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr82_1.1.1_javadoc", // Bluetooth
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr179-1_1-mrel-javadoc", // GPS
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr179_LocationUtil", // GPS Util
            "/home/ansel/Desktop/javadocs/jsr226", // M2G
            //"/home/ansel/Desktop/javadocs/jsr234", // AMMS
            "/home/ansel/Desktop/javadocs/jsr256", // Sensors
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr177", // SATS
            "/home/ansel/repos/j2me/J2ME_Docs/docs/jsr205", // SMS
        ];
        string target = "ms";

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

        Console.WriteLine("Read ok!");
        Console.WriteLine(
            $"Total: {classes.Count} classes, {classes.Values.Sum(x => x.methods.Length)} methods, {classes.Values.Sum(x => x.consts.Length)} constants, {classes.Values.Sum(x => x.fields.Length)} fields");
        if (classes.Values.GroupBy(x => x.fullName.hashCode()).Any(x => x.Count() != 1))
            throw new ArgumentException("Duplicated hash code!");

        int i = classes.Count;
        foreach (var cls in classes.Values)
        {
            Console.CursorLeft = 0;
            Console.Write($"{i} classes left...      ");
            LLMTools.Process(cls);
        }

        Console.CursorLeft = 0;
        Console.WriteLine("Analysis done!");

        models = classes.ToFrozenDictionary();

        switch (target)
        {
            case "print":
                new PrinterOutput().Accept("");
                break;
            case "nativejava":
                new JavaHeadersOutput(classes.ToFrozenDictionary()).Accept("/tmp/mbs/java");
                break;
            case "ms":
                new MidletSharpOutput(classes.ToFrozenDictionary()).Accept("/tmp/mbs/cs");
                break;
        }
    }
}