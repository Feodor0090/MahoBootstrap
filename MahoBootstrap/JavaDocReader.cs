using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MahoBootstrap.Models;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap;

public static class JavaDocReader
{
    private static readonly IBrowsingContext context = BrowsingContext.New(Configuration.Default);
    private static readonly IHtmlParser service = context.GetService<IHtmlParser>()!;

    public static ClassPrototype Parse(string document)
    {
        IDocument doc = service.ParseDocument(document);
        return Parse(doc.Body!.Children);
    }

    private static ClassPrototype Parse(IHtmlCollection<IElement> body)
    {
        ClassPrototype cp;
        {
            var title = body.SkipWhile(x => x.TagName != "H2").ToArray();
            cp = ParseDefinition(title.First(x => x.TagName == "H2"),
                title.SkipWhile(x => x.TagName != "HR").First(x => x.TagName == "DL"));
        }

        ParseFields(cp,
            body.SkipWhile(x => !IsFieldsBegin(x))
                .TakeWhile(x => !IsConstructorsBegin(x) && !IsMethodsBegin(x) && !IsNavBarBegin(x)).ToList());
        ParseConstructors(cp,
            body.SkipWhile(x => !IsConstructorsBegin(x))
                .TakeWhile(x => !IsMethodsBegin(x) && !IsNavBarBegin(x)).ToList());
        ParseMethods(cp,
            body.SkipWhile(x => !IsMethodsBegin(x))
                .TakeWhile(x => !IsNavBarBegin(x)).ToList());

        return cp;
    }

    public static void ApplyConstants(Dictionary<string, ClassModel> models, string constsDocument)
    {
        IDocument doc = service.ParseDocument(constsDocument);
        var list = doc.Body!.Children
            .SkipWhile(x => x.TagName != "HR")
            .TakeWhile(x => !IsNavBarBegin(x))
            .Where(x => x.TagName == "TABLE" && x.Children.Length == 1)
            .Select(x => x.Children[0])
            .Where(x => x.TagName == "TBODY" && x.Children.Length >= 2 && x.Children.All(y => y.TagName == "TR"))
            .Select(x => x.Children)
            .ToList();
        foreach (var item in list)
        {
            var header = item[0].Children[0];
            if (header.TagName != "TD")
                continue;
            if (header.Attributes["colspan"]?.Value != "3")
                continue;
            var className = header.TextContent.Trim();
            if (!models.TryGetValue(className, out var model))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Class {className} has constants but not parsed!");
                Console.WriteLine();
                Console.ResetColor();
                continue;
            }

            foreach (var line in item.Skip(1).Select(x => x.Children))
            {
                var decl = line[0].TextContent.Trim();
                var name = line[1].TextContent.Trim();
                var value = line[2].TextContent.Trim();
                model.consts.First(x => x.name == name).constantValue = value;
            }
        }
    }

    private static void ParseFields(ClassPrototype cp, List<IElement> list)
    {
        var en = list.GetEnumerator();
        while (true)
        {
            if (SkipToNextDefinition(ref en))
                return;

            var defTokens = ExtractDeclTokensFromPre(cp, ref en);

            var name = defTokens[^1];

            string type = "";
            int i;
            for (i = defTokens.Count - 2; i >= 0; i--)
            {
                type = defTokens[i] + type;
                if (defTokens[i][0] != '[')
                    break;
            }

            ParseModifiers(defTokens.Take(i), out var ma, out var mt);

            cp.fields.Add(new FieldPrototype
            {
                name = name,
                fieldType = type,
                memberType = mt,
                access = ma
            });
        }
    }

    private static void ParseConstructors(ClassPrototype cp, List<IElement> list)
    {
        var en = list.GetEnumerator();
        while (true)
        {
            if (SkipToNextDefinition(ref en))
                return;

            var defTokens = ExtractDeclTokensFromPre(cp, ref en);

            int openBracketPos = defTokens.IndexOf("(");
            ParseModifiers(defTokens.Take(openBracketPos - 1), out var ma, out var mt);

            var ctor = new CtorPrototype
            {
                access = ma
            };

            ParseMethodArgs(defTokens, ctor);
            ParseMethodThrows(defTokens, ctor);

            cp.constructors.Add(ctor);
        }
    }

    private static void ParseMethods(ClassPrototype cp, List<IElement> list)
    {
        var en = list.GetEnumerator();
        while (true)
        {
            if (SkipToNextDefinition(ref en))
                return;

            var defTokens = ExtractDeclTokensFromPre(cp, ref en);

            int openBracketPos = defTokens.IndexOf("(");

            int modsStart = openBracketPos - 2;
            string returnType;
            if (defTokens[modsStart][0] == '[')
            {
                returnType = defTokens[modsStart - 1] + defTokens[modsStart];
                modsStart--;
            }
            else
                returnType = defTokens[modsStart];

            ParseModifiers(defTokens.Take(modsStart), out var ma, out var mt);

            MethodPrototype mp = new MethodPrototype
            {
                name = defTokens[openBracketPos - 1],
                returnType = returnType,
                access = ma,
                type = mt,
            };

            ParseMethodArgs(defTokens, mp);
            ParseMethodThrows(defTokens, mp);

            cp.methods.Add(mp);
        }
    }

    private static void ParseMethodThrows(List<string> defTokens, ICodePrototype mp)
    {
        int closeBracketPos = defTokens.IndexOf(")");
        if (closeBracketPos < defTokens.Count - 1 && defTokens[closeBracketPos + 1] == "throws")
        {
            for (int i = closeBracketPos + 2; i < defTokens.Count; i++)
            {
                if (defTokens[i] != ",")
                    mp.throws.Add(defTokens[i]);
            }
        }
    }

    private static void ParseMethodArgs(List<string> defTokens, ICodePrototype mp)
    {
        int openBracketPos = defTokens.IndexOf("(");
        int closeBracketPos = defTokens.IndexOf(")");

        for (int i = openBracketPos + 1; i < closeBracketPos;)
        {
            var type = defTokens[i];
            i++;
            while (defTokens[i].StartsWith('['))
            {
                type += defTokens[i];
                i++;
            }

            var name = defTokens[i];
            i += 2; // skip comma
            mp.args.Add((type, name));
        }
    }

    private static void ParseModifiers(IEnumerable<string> mods, out MemberAccess ma, out MemberType mt)
    {
        ma = MemberAccess.Package;
        mt = MemberType.Regular;

        foreach (var mod in mods)
        {
            switch (mod)
            {
                case "public":
                    ma = MemberAccess.Public;
                    break;
                case "private":
                    ma = MemberAccess.Private;
                    break;
                case "protected":
                    ma = MemberAccess.Protected;
                    break;
                case "static":
                    mt |= MemberType.Static;
                    break;
                case "abstract":
                    mt |= MemberType.Abstract;
                    break;
                case "final":
                    mt |= MemberType.Final;
                    break;
            }
        }
    }

    private static List<string> ExtractDeclTokensFromPre(ClassPrototype cp, ref List<IElement>.Enumerator en)
    {
        var defNodes = en.Current.ChildNodes;
        List<string> defTokens = new();
        foreach (var defNode in defNodes)
        {
            if (defNode is IText tn)
            {
                var a = tn.Text.Replace(" ", " ").Replace("(", " ( ").Replace(")", " ) ").Replace(",", " , ")
                    .Replace("[", " [")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                defTokens.AddRange(a);
            }
            else if (defNode is IHtmlAnchorElement link)
            {
                defTokens.Add(GlobalizeReference(cp.pkg, link));
            }
            else if (defNode is HtmlElement he)
            {
                defTokens.Add(he.TextContent);
            }
        }

        return defTokens;
    }

    private static bool SkipToNextDefinition(ref List<IElement>.Enumerator en)
    {
        do
        {
            if (!en.MoveNext())
                return true;
        } while (en.Current.TagName != "H3");

        do
        {
            if (!en.MoveNext())
                return true;
        } while (en.Current.TagName != "PRE");

        return false;
    }

    private static ClassPrototype ParseDefinition(IElement h2Elem, IElement dlElem)
    {
        string pkg = h2Elem.Children[0].TextContent.Trim();
        string name = h2Elem.ChildNodes.Last().TextContent.Split(' ').Last().Trim();
        string modsLine = dlElem.Children[0].ChildNodes[0].TextContent;
        ClassType ct;
        if (modsLine.Contains("interface"))
            ct = ClassType.Interface;
        else if (modsLine.Contains("final"))
            ct = ClassType.Final;
        else if (modsLine.Contains("abstract"))
            ct = ClassType.Abstract;
        else
            ct = ClassType.Regular;

        string? parent = null;
        List<string> implements;

        if (ct == ClassType.Interface)
        {
            if (dlElem.Children.Length > 1)
                implements = dlElem.Children[1].Children.CollectClassRefs(pkg);
            else
                implements = new();
        }
        else
        {
            if (dlElem.Children.Length > 1)
                parent = dlElem.Children[1].Children.CollectClassRefs(pkg)[0];

            if (dlElem.Children.Length > 2)
                implements = dlElem.Children[2].Children.CollectClassRefs(pkg);
            else
                implements = new();
        }

        if (parent == "java.lang.Object")
            parent = null;

        ClassPrototype cp = new ClassPrototype(ct, pkg, name, parent);
        cp.implements.AddRange(implements);

        return cp;
    }

    private static List<string> CollectClassRefs(this IHtmlCollection<IElement> links, string pkg)
    {
        List<string> list = new List<string>();
        foreach (var e in links)
        {
            list.Add(GlobalizeReference(pkg, e));
        }

        return list;
    }

    private static string GlobalizeReference(string pkg, IElement link)
    {
        var href = link.Attributes["href"]!.Value;
        var fullPath = Path.GetFullPath(href, "/" + pkg.Replace('.', '/'));
        var className = fullPath[1..^5].Replace('/', '.');
        return className;
    }

    private static bool IsBegin(IElement elem, string name)
    {
        if (IsRef(elem))
            return elem.Attributes["name"]?.Value == name;
        if (elem.TagName == "P")
            return elem.Children.Any(x => IsBegin(x, name));
        return false;
    }

    private static bool IsFieldsBegin(IElement elem) => IsBegin(elem, "field_detail");

    private static bool IsConstructorsBegin(IElement elem) => IsBegin(elem, "constructor_detail");

    private static bool IsMethodsBegin(IElement elem) => IsBegin(elem, "method_detail");

    private static bool IsNavBarBegin(IElement elem) => IsBegin(elem, "navbar_bottom");

    private static bool IsRef(INode node) => node is IElement elem && elem.TagName == "A";
}