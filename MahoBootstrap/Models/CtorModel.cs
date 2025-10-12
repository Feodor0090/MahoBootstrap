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
}