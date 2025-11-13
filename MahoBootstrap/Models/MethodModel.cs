using System.Collections.Immutable;
using ikvm.extensions;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class MethodModel : CodeModel, IHashable, IHasHtmlDocs, IHasOwner
{
    public readonly string returnType;
    public readonly string name;
    public readonly MemberType type;
    public string htmlDocumentation { get; }

    public ClassModel? owner { get; set; }
    public MethodAnalysisData analysisData = new();

    public MethodModel(MethodPrototype mp) : base(mp)
    {
        returnType = mp.returnType;
        name = mp.name;
        type = mp.type;
        htmlDocumentation = string.Join('\n', mp.relevantDocPart.Select(x => x.OuterHtml));
    }

    public MethodModel(MemberAccess access, ImmutableArray<string> throws, ImmutableArray<CodeArgument> arguments,
        string returnType, string name, MemberType type, string docs) :
        base(access, throws, arguments)
    {
        this.returnType = returnType;
        this.name = name;
        this.type = type;
        htmlDocumentation = docs;
    }

    public MethodStyle methodStyle
    {
        get
        {
            if (name.StartsWith("get") && arguments.Length == 0 && returnType != "void")
                return MethodStyle.Getter;
            if (name.StartsWith("is") && arguments.Length == 0 && returnType != "void")
                return MethodStyle.Getter;
            if (name.StartsWith("set") && arguments.Length == 1 && returnType == "void")
                return MethodStyle.Setter;
            if (name == "size" && arguments.Length == 0)
                return MethodStyle.Getter;
            if (name == "get" && arguments.Length == 1 && arguments[0].type == "int" && returnType != "void")
                return MethodStyle.IndexGetter;
            if (name == "set" && arguments.Length == 2 && arguments[0].type == "int" && returnType == "void")
                return MethodStyle.IndexSetter;
            return MethodStyle.Regular;
        }
    }

    public string propertyType
    {
        get
        {
            switch (methodStyle)
            {
                case MethodStyle.Getter:
                    return returnType;
                case MethodStyle.Setter:
                    return arguments[0].type;
                case MethodStyle.IndexGetter:
                    return returnType;
                case MethodStyle.IndexSetter:
                    return arguments[1].type;
                default:
                    return null!;
            }
        }
    }

    public override string ToString()
    {
        return $"{returnType} {name}({string.Join(", ", arguments.Select(x => $"{x.type} {x.name}"))})";
    }


    public bool HasSameSignature(MethodModel other)
    {
        if (name != other.name)
            return false;
        if (returnType != other.returnType)
            return false;
        if (arguments.Length != other.arguments.Length)
            return false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (!arguments[i].type.Equals(other.arguments[i].type))
                return false;
        }

        return true;
    }

    public int stableHashCode
    {
        get
        {
            int c = 1;

            foreach (var arg in arguments)
            {
                c *= arg.type.hashCode();
            }

            return name.hashCode() * returnType.hashCode() * c * owner!.stableHashCode;
        }
    }

    public string dotnetMethodType
    {
        get
        {
            switch (type)
            {
                case MemberType.Regular:
                    if (owner?.parent != null)
                    {
                        if (Program.models.TryGetValue(owner.parent, out var parent))
                        {
                            if (parent.methods.Any(x => x.HasSameSignature(this)))
                            {
                                return "override";
                            }
                        }
                    }

                    return "virtual";
                case MemberType.Abstract:
                    return "abstract";
                case MemberType.Final:
                    return "";
                case MemberType.Static:
                case MemberType.Final | MemberType.Static:
                    return "static";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}

public enum MethodStyle
{
    Regular,
    Getter,
    Setter,
    IndexGetter,
    IndexSetter,
}