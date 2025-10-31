using System.Collections.Frozen;
using ikvm.extensions;
using MahoBootstrap.Models;
using MahoBootstrap.Outputs;

namespace MahoBootstrap;

internal class Program
{
    public static FrozenDictionary<string, ClassModel> models = null!;

    public const string LLM_CACHE_ROOT = "/home/ansel/mbs_cache";
    public const string OLLAMA_HOST = "http://127.0.0.1:11434";
    public const string MODEL = "deepseek-r1:14b";
    public const string DOCS_REPO = "/home/ansel/repos/j2me/J2ME_Docs/docs";
    public const string ARMAN_JDL = "/home/ansel/Desktop/javadocs";
    public const string MIDLET_SHARP_TARGET = "/tmp/mbs/cs";

    public static void Main(string[] args)
    {
        string[] docRoots =
        [
            Path.Combine(DOCS_REPO, "midp-2.0"), // MIDP
            Path.Combine(DOCS_REPO, "cldc-1.1"), // CLDC
            Path.Combine(DOCS_REPO, "jsr135"), // MMAPI
            Path.Combine(ARMAN_JDL, "jsr184"), // M3G
            Path.Combine(ARMAN_JDL, "nokiaui"), // NUI
            Path.Combine(DOCS_REPO, "jsr211"), // Content handler
            Path.Combine(ARMAN_JDL, "jsr75", "file"), // File system
            Path.Combine(ARMAN_JDL, "jsr75", "pim"), // Data system
            Path.Combine(ARMAN_JDL, "iapinfo"), // AP info
            Path.Combine(DOCS_REPO, "jsr82_1.1.1_javadoc"), // Bluetooth
            Path.Combine(DOCS_REPO, "jsr179-1_1-mrel-javadoc"), // GPS
            Path.Combine(DOCS_REPO, "jsr179_LocationUtil"), // GPS Util
            Path.Combine(ARMAN_JDL, "jsr226"), // M2G
            //"/home/ansel/Desktop/javadocs/jsr234", // AMMS
            Path.Combine(ARMAN_JDL, "jsr256"), // Sensors
            Path.Combine(DOCS_REPO, "jsr177"), // SATS
            Path.Combine(DOCS_REPO, "jsr205"), // SMS
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
                    if (file.Contains($"{Path.DirectorySeparatorChar}class-use{Path.DirectorySeparatorChar}"))
                        continue;
                    if (file.Contains($"doc-files{Path.DirectorySeparatorChar}"))
                        continue;
                    if (file.Contains("package-use.html"))
                        continue;
                    if (file.Contains("package-tree.html"))
                        continue;
                    if (file.Contains("package-frame.html"))
                        continue;
                    if (file.Contains("package-summary.html"))
                        continue;
                    if (file.Contains("copyright-notice.html"))
                        continue;
                    if (file.Contains("copyright.html"))
                        continue;
                    if (file.Contains($"{Path.DirectorySeparatorChar}index-files{Path.DirectorySeparatorChar}"))
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
                new MidletSharpOutput(classes.ToFrozenDictionary()).Accept(MIDLET_SHARP_TARGET);
                break;
        }
    }

    public static T? GetRandomElement<T>(ICollection<T> collection)
    {
        if (collection.Count == 0)
            return default;
        var index = Random.Shared.Next(collection.Count);
        return collection.ElementAt(index);
    }
}