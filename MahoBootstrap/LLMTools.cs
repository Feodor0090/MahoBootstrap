using System.Text;
using ikvm.extensions;
using MahoBootstrap.Models;
using Newtonsoft.Json;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace MahoBootstrap;

public static class LLMTools
{
    public const string JAVADOC_PROMPT = "Here is a \"rendered\" fragment of javadoc. " +
                                         "Reprint it as \"javadoc comment block\" so i could paste it in java source code. " +
                                         "Use \"@\" tags, avoid raw html. Do not output paragraph markup (i.e. <p></p>). " +
                                         "Use \"@link\" where feasible. Always print FULL type names, i.e. with packages. " +
                                         "If documentation says that method is inherited/overriden, use \"@inheritDoc\" if needed. " +
                                         "Output only new documentation comment. Do *not* wrap your answer in code block.";

    public static readonly (string, string)[] javadocExamples =
    [
        (REC_STORE_DOC_TEXT,
            "/**\n * Open (and possibly create) a record store associated with the\n" +
            " * given MIDlet suite. If this method is called by a MIDlet when\n" +
            " * the record store is already open by a MIDlet in the MIDlet suite,\n" +
            " * this method returns a reference to the same {@link javax.microedition.rms.RecordStore} object.\n" +
            " *\n" +
            " * @param recordStoreName the MIDlet suite unique name for the\n" +
            " *          record store, consisting of between one and 32 Unicode\n" +
            " *          characters inclusive.\n" +
            " * @param createIfNecessary if true, the record store will be\n" +
            " *                created if necessary\n" +
            " * @return {@link javax.microedition.rms.RecordStore} object for the record store\n" +
            " * @throws {@link javax.microedition.rms.RecordStoreException} if a record store-related\n" +
            " *                exception occurred\n" +
            " * @throws {@link javax.microedition.rms.RecordStoreNotFoundException} if the record store\n" +
            " *                could not be found\n" +
            " * @throws {@link javax.microedition.rms.RecordStoreFullException} if the operation cannot be\n" +
            " *                completed because the record store is full\n" +
            " * @throws {@link java.lang.IllegalArgumentException} if\n" +
            " *          recordStoreName is invalid\n" +
            " */")
    ];

    public const string XMLDOC_PROMPT = "Here is a fragment of javadoc. Print information from it as C# xmldoc. " +
                                        "Keep all type names, signatures and naming styles equal, " +
                                        "ignore languages (and standard libraries) differences. Always print FULL type names, i.e. with packages. " +
                                        "Ignore method overriding. Ignore docs inherition. Ignore \"since\" remark. Use <see cref=\"\"> to mention other class members and other types.\n\n" +
                                        "Output only new documentation comment. Do *not* wrap your answer in code block.";

    public static readonly (string, string)[] xmldocExamples =
    [
        (
            REC_STORE_DOC_TEXT,
            "/// <summary>\n/// Open (and possibly create) a record store associated with the given <see cref=\"javax.microedition.midlet.MIDlet\"/> suite.\n" +
            "/// If this method is called by a <see cref=\"javax.microedition.midlet.MIDlet\"/> when the record store is already open by a " +
            "<see cref=\"javax.microedition.midlet.MIDlet\"/> in the <see cref=\"javax.microedition.midlet.MIDlet\"/> suite,\n" +
            "/// this method returns a reference to the same <see cref=\"javax.microedition.rms.RecordStore\"/> object.\n" +
            "/// </summary>\n/// <param name=\"recordStoreName\">The <see cref=\"javax.microedition.midlet.MIDlet\"/> " +
            "suite unique name for the record store, consisting of between one and 32 Unicode characters inclusive.</param>\n" +
            "/// <param name=\"createIfNecessary\">If <see langword=\"true\"/>, the record store will be created if necessary.</param>\n" +
            "/// <returns><see cref=\"javax.microedition.rms.RecordStore\"/> object for the record store.</returns>\n" +
            "/// <exception cref=\"javax.microedition.rms.RecordStoreException\">If a record store-related exception occurred.</exception>\n" +
            "/// <exception cref=\"javax.microedition.rms.RecordStoreNotFoundException\">If the record store could not be found.</exception>\n" +
            "/// <exception cref=\"javax.microedition.rms.RecordStoreFullException\">If the operation cannot be completed because the record store is full.</exception>\n" +
            "/// <exception cref=\"java.lang.IllegalArgumentException\">If <paramref name=\"recordStoreName\"/> is invalid.</exception>")
    ];

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

    public const string NULLABLE_PROMPT = "Here is a javadoc for a method in a class. Analyze it. " +
                                          "For each parameter and return type, understand, can it accept/return null values?\n" +
                                          "- Threat `void` as never null.\n" +
                                          "- Threat promitive types (i.e. integers, chars) as never null.\n" +
                                          "- For reference types, including boxed primitives and arrays, " +
                                          "look at defined constrains in method description and thrown exceptions: method may block invalid values using exceptions.\n\n" +
                                          "Answer in following format:\n```\n{\n\t\"return\": true,\n\t\"param1\": false,\n\t\"param2\": true,\n\t...\n}\n```\n\n" +
                                          "...where \"true\" is \"parameter accepts null or retuned value can be null\", \"false\" otherwise.\n" +
                                          "Follow the format exactly, i.e. code block with json object. For parameter key names, use parameter's names from documentation.";

    public static readonly (string, string)[] nullableExamples =
    [
        ("<pre>public int <b>getIndexCount</b>()</pre>\n<dl>\n<dd><span class=\"new\">Returns the number of indices in this buffer.\n" +
         " This many indices will be returned in a <code>getIndices</code>\n call.  " +
         "The number of indices returned depends on the type of\n low-level rendering primitives in the buffer: Currently, only\n " +
         "triangles are supported, and there are three indices per\n triangle. Triangles are counted individually, disregarding\n " +
         "triangle strips.</span>\n\n <p class=\"new\">Note that implementations are allowed to\n optimize the index data internally. " +
         "Different implementations\n may therefore report slightly different index counts for the\n same set of input primitives.</p>\n" +
         "<p>\n</p></dd><dd><dl>\n\n<dt><b>Returns:</b></dt><dd><span class=\"new\">the number of indices</span></dd><dt><b>Since:</b></dt>\n  " +
         "<dd><span class=\"new\">M3G 1.1</span></dd>\n<dt><b>See Also:</b></dt><dd>" +
         "<a href=\"../../../javax/microedition/m3g/IndexBuffer.html#getIndices(int[])\">" +
         "<code><span class=\"new\">getIndices</span></code></a></dd></dl>\n</dd>\n</dl>",
            "```json\n{\n    \"return\": false\n}\n```"),
        ("<pre>\n\npublic <a href=\"../../../java/lang/String.html\" title=\"class in java.lang\">String</a> <b>getType</b>()</pre>\n<dl>\n\n" +
         "<dd>Returns the type of content that the resource connected to is\n providing.  For instance, if the connection is via HTTP, then\n" +
         " the value of the <code>content-type</code> header field is returned.\n\n<p>\n\n</p></dd><dd><dl>\n\n</dl>\n\n</dd>\n\n<dd><dl>\n\n\n" +
         "<dt><b>Returns:</b></dt><dd>the content type of the resource that the URL references,\n" +
         "          or <code>null</code> if not known.</dd></dl>\n\n</dd>\n\n</dl>",
            "```json\n{\n  \"return\": true\n}\n```"),
        ("<pre>public void <b>getBoneTransform</b>" +
         "(<a href=\"../../../javax/microedition/m3g/Node.html\" title=\"class in javax.microedition.m3g\">Node</a>&nbsp;bone,\n" +
         "                             <a href=\"../../../javax/microedition/m3g/Transform.html\" title=\"class in javax.microedition.m3g\">" +
         "Transform</a>&nbsp;transform)</pre>\n<dl>\n<dd><span class=\"new\">Returns the at-rest transformation for a bone\n node.  " +
         "This is the transformation stored in <code>addTransform</code>\n as described in the documentation there.</span>\n\n " +
         "<p class=\"new\">If the given node is in the skeleton group of\n this Mesh, but has no vertices associated with it according to\n " +
         "<code>getBoneVertices</code>, the returned transformation is\n undefined.</p>\n<p>\n</p></dd><dd><dl>\n<dt><b>Parameters:</b></dt>" +
         "<dd><code>bone</code> - <span class=\"new\">the bone node</span></dd><dd><code>transform</code> - <span class=\"new\">" +
         "the Transform object to\n        receive the bone transformation</span>\n</dd><dt><b>Throws:</b>\n</dt>" +
         "<dd><code>java.lang.NullPointerException</code> - <span class=\"new\">if <code>bone</code>\n         is null</span>\n</dd>" +
         "<dd><code>java.lang.NullPointerException</code> - <span class=\"new\">if <code>transform</code>\n         is null</span>\n</dd>" +
         "<dd><code>java.lang.IllegalArgumentException</code> - <span class=\"new\">if <code>bone</code>\n         " +
         "is not in the skeleton group of this mesh</span></dd><dt><b>Since:</b></dt>\n  <dd><span class=\"new\">M3G 1.1</span></dd>\n" +
         "<dt><b>See Also:</b></dt><dd><a href=\"../../../javax/microedition/m3g/SkinnedMesh.html#getBoneVertices(javax.microedition.m3g.Node, " +
         "int[], float[])\"><code><span class=\"new\">getBoneVertices</span></code></a>, \n" +
         "<a href=\"../../../javax/microedition/m3g/SkinnedMesh.html#addTransform(javax.microedition.m3g.Node, int, int, int)\">" +
         "<code><span class=\"new\">addTransform</span></code></a></dd></dl>\n</dd>\n</dl>",
            "```json\n{\n    \"return\": false,\n    \"bone\": false,\n    \"transform\": false\n}\n```"),
        ("<pre>\n\npublic <a href=\"../../../java/lang/String.html\" title=\"class in java.lang\">String</a> <b>getContentType</b>()</pre>\n" +
         "<dl>\n\n<dd>Get the content type of the media that's\n being played back by this <code>Player</code>.\n <p>\n See " +
         "<a href=\"Manager.html#content-type\">content type</a>\n for the syntax of the content type returned.\n\n</p>" +
         "<p>\n\n</p></dd><dd><dl>\n\n</dl>\n\n</dd>\n\n<dd><dl>\n\n\n<dt><b>Returns:</b></dt>" +
         "<dd>The content type being played back by this \n <code>Player</code>.\n</dd><dt><b>Throws:</b>\n</dt>" +
         "<dd><code><a href=\"../../../java/lang/IllegalStateException.html\" title=\"class in java.lang\">IllegalStateException</a>" +
         "</code> - Thrown if the <code>Player</code>\n is in the <i>UNREALIZED</i> or <i>CLOSED</i> state.</dd></dl>\n\n</dd>\n\n</dl>",
            "```json\n{\n    \"return\": false\n}\n```"),
        ("<pre>public void <b>setPayloadText</b>(java.lang.String&nbsp;data)</pre>\n<dl>\n<dd>Sets the payload data of this message. " +
         "The payload data may be <code>null</code>.\n<p>\n</p></dd><dd><dl>\n</dl>\n</dd>\n<dd><dl>\n<dt><b>Parameters:</b></dt>" +
         "<dd><code>data</code> - payload data as a <code>String</code></dd><dt><b>See Also:</b></dt>" +
         "<dd><a href=\"../../../javax/wireless/messaging/TextMessage.html#getPayloadText()\"><code>getPayloadText()</code>" +
         "</a></dd></dl>\n</dd>\n</dl>",
            "```json\n{\n    \"return\": false,\n    \"data\": true\n}\n```\n")
    ];

    public const string REC_STORE_DOC_TEXT =
        "<pre>\n\npublic static <a href=\"../../../javax/microedition/rms/RecordStore.html\" title=\"class in javax.microedition.rms\">RecordStore</a> " +
        "<b>openRecordStore</b>(<a href=\"../../../java/lang/String.html\" title=\"class in java.lang\">String</a>&nbsp;recordStoreName,\n" +
        "                                          boolean&nbsp;createIfNecessary)\n" +
        "                                   throws <a href=\"../../../javax/microedition/rms/RecordStoreException.html\" title=\"class in javax.microedition.rms\">RecordStoreException</a>,\n" +
        "                                          <a href=\"../../../javax/microedition/rms/RecordStoreFullException.html\" title=\"class in javax.microedition.rms\">RecordStoreFullException</a>,\n" +
        "                                          <a href=\"../../../javax/microedition/rms/RecordStoreNotFoundException.html\" title=\"class in javax.microedition.rms\">RecordStoreNotFoundException</a></pre>\n<dl>\n\n" +
        "<dd>Open (and possibly create) a record store associated with the\n given MIDlet suite. If this method is called by a MIDlet when\n the record store is already open by a MIDlet in the MIDlet suite,\n" +
        " this method returns a reference to the same RecordStore object.\n\n<p>\n\n</p></dd><dd><dl>\n\n<dt><b>Parameters:</b></dt><dd><code>recordStoreName</code> - the MIDlet suite unique name for the\n" +
        "          record store, consisting of between one and 32 Unicode\n          characters inclusive.</dd><dd><code>createIfNecessary</code> - if true, the record store will be\n" +
        "\t\tcreated if necessary\n</dd><dt><b>Returns:</b></dt><dd><code>RecordStore</code> object for the record store\n</dd><dt><b>Throws:</b>\n" +
        "</dt><dd><code><a href=\"../../../javax/microedition/rms/RecordStoreException.html\" title=\"class in javax.microedition.rms\">RecordStoreException</a></code> - if a record store-related\n" +
        "\t\texception occurred\n</dd><dd><code><a href=\"../../../javax/microedition/rms/RecordStoreNotFoundException.html\" title=\"class in javax.microedition.rms\">RecordStoreNotFoundException</a></code> - if the record store" +
        "\n\t\tcould not be found\n</dd><dd><code><a href=\"../../../javax/microedition/rms/RecordStoreFullException.html\" title=\"class in javax.microedition.rms\">RecordStoreFullException</a></code> - if the operation cannot be" +
        "\n\t\tcompleted because the record store is full\n</dd><dd><code><a href=\"../../../java/lang/IllegalArgumentException.html\" title=\"class in java.lang\">IllegalArgumentException</a></code> - if\n" +
        "          recordStoreName is invalid</dd></dl>\n\n</dd>\n\n</dl>";

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
               $"exclusive enum ImageDepth {{ DEPTH_16B, DEPTH_24B, DEPTH_32B }}\nAccepted in methods: encodeImage\nReturned by: -\n\n" +
               $"exclusive enum DrawDepth {{ DEPTH_16B, DEPTH_24B }}\nAccepted in methods: drawPixels\nReturned by: -\n\n" +
               $"flags enum CoderHints {{ PREFER_QUALITY, DITHER }}\nAccepted in methods: <constructor>\nReturned by: getCoderHints\n\n" +
               $"Could not group: MAX_WIDTH, MAX_HEIGHT\n```\n";
    }

    public static void Process(ClassModel model)
    {
        ThinkValue fast = new ThinkValue(true); // ThinkValue.Medium; for GPT-OSS
        ThinkValue slow = new ThinkValue(true); // ThinkValue.High;

        foreach (var method in model.methods)
        {
            MethodAnalysisData mad = new();
            Func<string, string> parser = static x => x;
            mad.javadoc = GetAuto(new Prompt(JAVADOC_PROMPT, javadocExamples), method, parser, fast);
            mad.xmldoc = GetAuto(new Prompt(XMLDOC_PROMPT, xmldocExamples), method, parser, fast);
            mad.effect = GetAuto(IMPL_PROMPT, method, x =>
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
            }, fast);

            mad.alwaysThrows = GetAuto(ALWAYS_THROWS_PROMPT, method, x =>
            {
                if (x.ToLower().Contains("regular method"))
                    return (string?)null;
                var t = x.Trim();
                if (Program.models.ContainsKey(t))
                    return t;
                throw new KeyNotFoundException();
            }, fast);

            if (CantBeNull(method.returnType) &&
                (method.arguments.Length == 0 || method.arguments.All(x => CantBeNull(x.type))))
            {
                Dictionary<string, bool> map = new();
                map["return"] = false;
                foreach (var arg in method.arguments)
                {
                    map[arg.name] = false;
                }

                mad.nullability = map;
            }
            else
            {
                mad.nullability = GetAuto(new Prompt(NULLABLE_PROMPT, nullableExamples), method, x =>
                {
                    var lines = x.Split('\n').Select(y => y.Trim()).Where(y => y[0] != '`');
                    return JsonConvert.DeserializeObject<Dictionary<string, bool>>(string.Join("", lines))!;
                }, fast);
            }

            method.analysisData = mad;
        }

        ClassAnalysisData cad = new();
        cad.listAPI = GetAuto(LIST_PROMPT, model, x =>
        {
            var lines = x.Split('\n').Select(y => y.Trim()).Where(y => y[0] != '`');
            return JsonConvert.DeserializeObject<ListAPI[]>(string.Join("", lines))!;
        }, slow);
        if (model.consts.Any())
        {
            (cad.groupedEnums, cad.keptConsts) = GetAuto(ComposeEnumPrompt(model.consts.Select(x => x.name).ToList()),
                model, ParseEnumProposal, slow);
        }
        else
        {
            cad.groupedEnums = [];
            cad.keptConsts = [];
        }
    }

    public static void ClearCache()
    {
        Directory.Delete(Program.LLM_CACHE_ROOT, true);
    }

    private static TOut GetAuto<TIn, TOut>(Prompt prompt, TIn target, Func<string, TOut> parser, ThinkValue tv)
        where TIn : IHashable, IHasHtmlDocs
    {
        string folderName = Path.Combine(Program.LLM_CACHE_ROOT, $"{(uint)prompt.system.hashCode()}");
        string cacheFileName = Path.Combine(folderName, $"{target.stableHashCode}");
        if (File.Exists($"{cacheFileName}.txt"))
        {
            return parser(File.ReadAllText($"{cacheFileName}.txt"));
        }

        Directory.CreateDirectory(folderName);
        while (true)
        {
            var generated = Request(prompt, target.htmlDocumentation, tv);
            var timeId = DateTime.Now.ToString().Replace(':', '.').Replace(' ', '_');
            File.WriteAllText($"{cacheFileName}_thinking_{timeId}.txt", generated.thinking);
            TOut result;
            try
            {
                result = parser(generated.answer);
            }
            catch
            {
                File.WriteAllText($"{cacheFileName}_broken_{timeId}.txt", generated.answer);
                continue;
            }

            File.WriteAllText($"{cacheFileName}.txt", generated.answer);
            return result;
        }
    }

    private static (string thinking, string answer) Request(Prompt prompt, string data, ThinkValue tv)
    {
        var ollama = new OllamaApiClient(new Uri(Program.OLLAMA_HOST));
        ollama.SelectedModel = Program.MODEL;

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

    public static (GroupedEnum[], string[]) ParseEnumProposal(string input)
    {
        const string exclusivePrefix = "exclusive enum ";
        const string flagsPrefix = "flags enum ";
        const string enumDelimiterStart = "{";
        const string enumDelimiterEnd = "}";
        const string acceptedPrefix = "Accepted in methods: ";
        const string returnedPrefix = "Returned by: ";
        const string couldNotGroupPrefix = "Could not group: ";
        const string noneValue = "-";
        const char itemSeparator = ',';

        var lines = input.Split('\n');
        var groups = new List<GroupedEnum>();
        string[] couldNotGroup = Array.Empty<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith(exclusivePrefix) || line.StartsWith(flagsPrefix))
            {
                bool isFlags = line.StartsWith(flagsPrefix);
                string prefix = isFlags ? flagsPrefix : exclusivePrefix;
                int prefixLength = prefix.Length;

                int enumStartIndex = line.IndexOf(enumDelimiterStart, prefixLength);
                if (enumStartIndex == -1) throw new FormatException("Invalid enum format: missing start delimiter.");

                string name = line.Substring(prefixLength, enumStartIndex - prefixLength).Trim();

                int enumEndIndex = line.IndexOf(enumDelimiterEnd, enumStartIndex + enumDelimiterStart.Length);
                if (enumEndIndex == -1) throw new FormatException("Invalid enum format: missing end delimiter.");

                string membersStr = line.Substring(enumStartIndex + enumDelimiterStart.Length,
                    enumEndIndex - (enumStartIndex + enumDelimiterStart.Length)).Trim();
                string[] members = membersStr.Split(itemSeparator).Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m)).ToArray();

                // Next line: Accepted
                i++;
                if (i >= lines.Length) throw new FormatException("Unexpected end of input after enum declaration.");
                string acceptedLine = lines[i].Trim();
                if (!acceptedLine.StartsWith(acceptedPrefix))
                    throw new FormatException("Expected 'Accepted in methods' line.");
                string acceptedStr = acceptedLine.Substring(acceptedPrefix.Length).Trim();
                string[] usedIn = acceptedStr == noneValue
                    ? Array.Empty<string>()
                    : acceptedStr.Split(itemSeparator).Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m))
                        .ToArray();

                // Next line: Returned
                i++;
                if (i >= lines.Length) throw new FormatException("Unexpected end of input after accepted line.");
                string returnedLine = lines[i].Trim();
                if (!returnedLine.StartsWith(returnedPrefix)) throw new FormatException("Expected 'Returned by' line.");
                string returnedStr = returnedLine.Substring(returnedPrefix.Length).Trim();
                string[] returnedIn = returnedStr == noneValue
                    ? Array.Empty<string>()
                    : returnedStr.Split(itemSeparator).Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m))
                        .ToArray();

                groups.Add(new GroupedEnum
                {
                    flags = isFlags,
                    name = name,
                    members = members,
                    usedInMethods = usedIn,
                    returnedInMethods = returnedIn
                });
            }
            else if (line.StartsWith(couldNotGroupPrefix))
            {
                string cngStr = line.Substring(couldNotGroupPrefix.Length).Trim();
                couldNotGroup = cngStr.Split(itemSeparator).Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m))
                    .ToArray();
            }
            else
            {
                throw new FormatException($"Unexpected line: {line}");
            }
        }

        return (groups.ToArray(), couldNotGroup);
    }

    public static bool CantBeNull(string type)
    {
        switch (type)
        {
            case "void":
            case "byte":
            case "boolean":
            case "short":
            case "char":
            case "int":
            case "long":
            case "float":
            case "double":
                return true;
            default:
                return false;
        }
    }
}