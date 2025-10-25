using System.Collections.Immutable;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class MethodModel : CodeModel
{
    public readonly string returnType;
    public readonly string name;
    public readonly MemberType type;
    public ClassModel? owner;

    public MethodModel(MethodPrototype mp) : base(mp)
    {
        returnType = mp.returnType;
        name = mp.name;
        type = mp.type;
    }

    public MethodModel(MemberAccess access, ImmutableArray<string> throws, ImmutableArray<CodeArgument> arguments,
        string returnType, string name, MemberType type) :
        base(access, throws, arguments)
    {
        this.returnType = returnType;
        this.name = name;
        this.type = type;
    }

    public MethodStyle MethodStyle
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

    public string PropertyType
    {
        get
        {
            switch (MethodStyle)
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
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public override string ToString()
    {
        return $"{returnType} {name}()";
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