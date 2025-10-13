using System.Collections.Immutable;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class CtorModel : CodeModel
{
    public CtorModel(CtorPrototype proto) : base(proto)
    {
    }

    public CtorModel(MemberAccess access, ImmutableArray<string> throws, ImmutableArray<CodeArgument> arguments) :
        base(access, throws, arguments)
    {
    }

    public bool HasSameSignature(CtorModel other)
    {
        if (arguments.Length != other.arguments.Length)
            return false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (!arguments[i].type.Equals(other.arguments[i].type))
                return false;
        }

        return true;
    }

    public bool HasSameSignature(List<CodeArgument> args)
    {
        if (arguments.Length != args.Count)
            return false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (!arguments[i].type.Equals(args[i].type))
                return false;
        }

        return true;
    }
}