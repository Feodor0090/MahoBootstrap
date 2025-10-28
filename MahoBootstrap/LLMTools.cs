using System.Text;
using ikvm.extensions;
using MahoBootstrap.Models;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace MahoBootstrap;

public static class LLMTools
{
    public const string JAVADOC_PROMPT = "Here is a \"rendered\" fragment of javadoc. " +
                                         "Reprint it as \"javadoc comment block\" so i could paste it in java source code. " +
                                         "Use \"@link\" where feasible. Output only new documentation comment. Do *not* wrap your answer in code block.";

    public const string XMLDOC_PROMPT = "Here is a fragment of javadoc. Print information from it as C# xmldoc. " +
                                        "Keep all type names, signatures and naming styles equal, " +
                                        "ignore languages (and standard libraries) differences. Always print FULL type names, i.e. with packages. " +
                                        "Ignore method overriding. Ignore docs inherition. Ignore \"since\" remark. Use <see cref=\"\"> to mention other class members and other types.\n\n" +
                                        "Output only new documentation comment. Do *not* wrap your answer in code block.";

    public const string EMPTY_IMPL_PROMPT = "Here is a fragment of javadoc. Analyze it.\n\n" +
                                            "You must understand, does this method do anything when called as is or is its implementation empty - " +
                                            "sometimes programmer may need to override some methods to implement callbacks, etc. " +
                                            "Throwing exceptions, returning values are examples of work. " +
                                            "Answer \"does nothing\" only when documentation states that calling this method is no-op and has no sideeffects at all.\n\n" +
                                            "Answer with one phrase: either \"Does something\" or \"Does nothing\".";

    public const string ALWAYS_THROWS_PROMPT = "Here is a fragment of javadoc. Analyze it. " +
                                               "You must understand: does this method *always* throw exception when called or not?\n\n" +
                                               "If not, answer with one phrase: \"Regular method\".\n\n" +
                                               "If yes, print full exception type as an answer, for example, \"java.pkg.InvalidSomethingException\".\n\n" +
                                               "Do not provide additional information to user. Do not wrap your answer in code or formatting blocks.";

    public const string LIST_PROMPT = "";

    public static string ComposeEnumPrompt(List<string> constNames)
    {
        return $"Act as software architector.\n" +
               $"Look through a provided document for the class. " +
               $"Your goal is to improve this API by introducing enums instead of integer constants where possible.\n\n" +
               $"The class has following constants: {string.Join(", ", constNames)} .\n" +
               $"You need to determine, can these constants replaced with enums or not.\n\n" +
               $"If a constant is not used in any method, it _can not be replaced_.\n" +
               $"If a method accepts as an argument (or returns) a list of mentioned constants _and only them_, they _can be replaced_. " +
               $"If constants can be combined via logical OR, threat them as \"flags\", \"exclusive\" otherwise. " +
               $"You also need to come up with a name for the enum. " +
               $"An example of \"accepts only them\" may be mentioning in method's docs like \"one of CONST1, CONST2 or CONST3\" or throwing an exception for any other values.\n" +
               $"If a method accepts as an argument (or returns) a list of mentioned constants _or any other integer_, they _can not be replaced_. " +
               $"If such constants can be grouped into enums from other methods usages, _mention them in answer both as enum members and \"ungroupable\" constants_.\n\n" +
               $"There can be situation where some methods accept a different list of same constants, " +
               $"for example, \"method1\" accepts constants \"type1\" and \"type2\", while method2 accepts \"type2\" and \"type3\". " +
               $"This is valid, values can be dublicated in different enums. In such case imagine names for two enums and use them.\n\n" +
               $"In your answer:\n\n" +
               $"1) Enumerate found enums with their names, names of included constants (order does not matter), " +
               $"names of methods (do not print full signature, only names; if there is a constructor, mention it with `<constructor>`) that accept this enum.\n" +
               $"2) If there are constants that can't be grouped in any enum, enumerate them.\n" +
               $"3) Do not additional info and mapping of values.\n\n" +
               $"Strictly follow an example:\n```\n" +
               $"exclusive enum ImageType {{ PNG, JPEG, BMP }}\nAccepted in methods: decodeImage, encodeImage\nReturned by: getImageFormat\n\n" +
               $"exclusive enum ImageDepth {{ DEPTH_16B, DEPTH_24B, DEPTH_32B }}\nUsed in methods: encodeImage\nReturned by: -\n\n" +
               $"exclusive enum DrawDepth {{ DEPTH_16B, DEPTH_24B }}\nUsed in methods: drawPixels\nReturned by: -\n\n" +
               $"flags enum CoderHints {{ PREFER_QUALITY, DITHER }}\nUsed in methods: <constructor>\nReturned by: getCoderHints\n\n" +
               $"Could not group: MAX_WIDTH, MAX_HEIGHT\n```\n";
    }

    public const string cacheRoot = "/home/ansel/mbs_cache";

    public static void Process(ClassModel model)
    {
        foreach (var method in model.methods)
        {
            MethodAnalysisData mad = new();
            mad.javadoc = GetAuto(JAVADOC_PROMPT, method, static x => x.StableHashCode, static x => x.documentation,
                ThinkValue.Medium);
            mad.xmldoc = GetAuto(XMLDOC_PROMPT, method, static x => x.StableHashCode, static x => x.documentation,
                ThinkValue.Medium);
            mad.empty = GetAuto(EMPTY_IMPL_PROMPT, method, static x => x.StableHashCode, static x => x.documentation,
                    ThinkValue.Medium).ToLower()
                .Contains("nothing");

            var thr = GetAuto(ALWAYS_THROWS_PROMPT, method, static x => x.StableHashCode, static x => x.documentation,
                ThinkValue.Medium);
            if (thr.ToLower().Contains("regular method"))
                mad.alwaysThrows = null;
            else
                mad.alwaysThrows = thr;
        }
    }

    public static void ClearCache()
    {
        Directory.Delete(cacheRoot, true);
    }

    private static string GetAuto<T>(string system, T target, Func<T, int> hash, Func<T, string> printer, ThinkValue tv)
        where T : notnull
    {
        string folderName = Path.Combine(cacheRoot, $"{(uint)system.hashCode()}");
        string cacheFileName = Path.Combine(folderName, $"{hash(target)}");
        if (File.Exists($"{cacheFileName}.txt"))
        {
            return File.ReadAllText($"{cacheFileName}.txt");
        }

        var generated = Request(system, printer(target), tv);
        Directory.CreateDirectory(folderName);
        File.WriteAllText($"{cacheFileName}.txt", generated.answer);
        File.WriteAllText($"{cacheFileName}_thinking.txt", generated.thinking);
        return generated.answer;
    }

    private static (string thinking, string answer) Request(string system, string data, ThinkValue tv)
    {
        var ollama = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
        ollama.SelectedModel = "gpt-oss:20b";

        var messages = new List<Message>
        {
            new Message(ChatRole.System, system),
            new Message(ChatRole.User, data)
        };

        var request = new ChatRequest
        {
            Model = ollama.SelectedModel,
            Messages = messages,
            Think = tv
        };

        var chatTask = Task.Run(async () =>
        {
            StringBuilder think = new();
            StringBuilder final = new();

            await foreach (var stream in ollama.ChatAsync(request))
            {
                think.Append(stream?.Message.Thinking);
                final.Append(stream?.Message.Content);
            }

            return (think.ToString(), final.ToString());
        });
        return chatTask.Result;
    }
}