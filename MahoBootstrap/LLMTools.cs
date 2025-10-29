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

    public const string IMPL_PROMPT = "Here is a fragment of javadoc. Analyze it.\n\n" +
                                      "You must understand, does this method do anything when called as is or is its implementation empty - " +
                                      "sometimes programmer may need to override some methods to implement callbacks, etc. Then, understand - may calling this method change object's state? " +
                                      "Always throwing exceptions, returning values (without modifing them) are examples of \"pure\" work.\n\n" +
                                      "Answer \"does nothing\" if documentation states that this method does nothing when called (no-op).\n" +
                                      "Answer \"pure method\" if documentation states that method does something but has *no* sideeffects: it just returns a value, checks something, throws, etc.\n" +
                                      "Answer \"has side effects\" in other cases.\n\n" +
                                      "Answer with one phrase: \"Has side effects\", \"Pure method\" or \"Does nothing\". Do not add anything else to answer.";

    public const string ALWAYS_THROWS_PROMPT = "Here is a fragment of javadoc. Analyze it. " +
                                               "You must understand: does this method *always* throw exception when called or not?\n\n" +
                                               "If not, answer with one phrase: \"Regular method\".\n\n" +
                                               "If yes, print full exception type as an answer, for example, \"java.pkg.InvalidSomethingException\".\n\n" +
                                               "Do not provide additional information to user. Do not wrap your answer in code or formatting blocks.";

    public const string LIST_PROMPT = "Here is a javadoc for a class. Analyze it. " +
                                      "Does it look like a container for some child items? " +
                                      "If no, answer with one phrase: \"Not a list\".\n\n" +
                                      "If it seems so, find method signatures that allow to interact with object's children.\n\n" +
                                      "Answer in the following JSON format:\n```\n" +
                                      "[\n{\n  \"type\": \"pkg.Class1\",\n" +
                                      "  \"operation1\": \"Full.Return.Type.Signature fullMethodSignature(Full.Argument.Type arg1name, Another.Full.Argument.Type arg2)\",\n" +
                                      "  \"operation2\": \"void insert(int position, pkg.Class1 child)\",\n" +
                                      "  \"operation3\": ...,\n  ...\n},\n{ ... }\n]" +
                                      "\n```\n\n" +
                                      "Follow it exactly: the code block, the array, an object declaration for each list \"inside\".\n\n" +
                                      "Find methods for following operations:\n" +
                                      "- Getting a child by index (`get`)\n" +
                                      "- Setting a child by index (`set`)\n" +
                                      "- Adding new child to the end of list (`add`)\n" +
                                      "- Insert after index (`insert`)\n" +
                                      "- Remove at index (`remove`)\n" +
                                      "- Remove all (`clear`)\n" +
                                      "- Get enumerator to enumerate all children (`enum`) \n" +
                                      "- Get count (`count`)\n\n" +
                                      "If no method found for an operation, it is valid, write as `\"operation\": null`. " +
                                      "If you can't find most of methods including important ones (like `get`), may be, this class is not a list?\n\n" +
                                      "In the case when there are multiple methods for the same operation, look at accepted/returned types. Non-matching ones are helpers, ignore them.\n\n" +
                                      "In the case when class operates a list of tuples (`set` accepts multiple values, there are separate getters for each one), threat the class as *not a list*.\n\n" +
                                      "In the case when class clearly keeps multiple child lists of different types, repeat for each one - in answer example i left a declaration for multiple lists.\n\n";

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
            Func<MethodModel, string> printer = static x => x.documentation;
            Func<string, string> parser = static x => x;
            mad.javadoc = GetAuto(JAVADOC_PROMPT, method, printer, parser, ThinkValue.Medium);
            mad.xmldoc = GetAuto(XMLDOC_PROMPT, method, printer, parser, ThinkValue.Medium);
            mad.effect = GetAuto(IMPL_PROMPT, method, printer, x =>
            {
                var l = x.ToLower().Trim();
                switch (l)
                {
                    case "does nothing":
                        return MethodEffect.Empty;
                    case "pure method":
                        return MethodEffect.Pure;
                    case "has side effects":
                        return MethodEffect.HasSideEffects;
                    default:
                        throw new ArgumentException();
                }
            }, ThinkValue.Medium);

            mad.alwaysThrows = GetAuto(ALWAYS_THROWS_PROMPT, method, printer, x =>
            {
                if (x.ToLower().Contains("regular method"))
                    return (string?)null;
                var t = x.Trim();
                if (Program.models.ContainsKey(t))
                    return t;
                throw new KeyNotFoundException();
            }, ThinkValue.Medium);

            method.analysisData = mad;
        }


    }

    public static void ClearCache()
    {
        Directory.Delete(cacheRoot, true);
    }

    private static TOut GetAuto<TIn, TOut>(Prompt prompt, TIn target, Func<TIn, string> printer,
        Func<string, TOut> parser, ThinkValue tv)
        where TIn : IHashable
    {
        string folderName = Path.Combine(cacheRoot, $"{(uint)prompt.system.hashCode()}");
        string cacheFileName = Path.Combine(folderName, $"{target.stableHashCode}");
        if (File.Exists($"{cacheFileName}.txt"))
        {
            return parser(File.ReadAllText($"{cacheFileName}.txt"));
        }

        Directory.CreateDirectory(folderName);
        while (true)
        {
            var generated = Request(prompt, printer(target), tv);
            File.WriteAllText($"{cacheFileName}_thinking_{DateTime.Now}.txt", generated.thinking);
            TOut result;
            try
            {
                result = parser(generated.answer);
            }
            catch
            {
                continue;
            }

            File.WriteAllText($"{cacheFileName}.txt", generated.answer);
            return result;
        }
    }

    private static (string thinking, string answer) Request(Prompt prompt, string data, ThinkValue tv)
    {
        var ollama = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
        ollama.SelectedModel = "gpt-oss:20b";

        var messages = new List<Message>
        {
            new Message(ChatRole.System, prompt.system)
        };
        foreach (var example in prompt.examples)
        {
            messages.Add(new Message(ChatRole.User, example.Item1));
            messages.Add(new Message(ChatRole.Assistant, example.Item2));
        }
        messages.Add(new Message(ChatRole.User, data));

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