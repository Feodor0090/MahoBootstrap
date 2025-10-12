using System.Collections.Immutable;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class MethodModel : CodeModel
{
    public readonly string returnType;
    public readonly string name;
    public readonly MemberType type;

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
}