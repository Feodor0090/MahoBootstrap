using System.Collections.Immutable;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class CodeModel : ModelBase
{
    public readonly ImmutableArray<string> throws;
    public readonly ImmutableArray<CodeArgument> arguments;

    protected CodeModel(ICodePrototype proto) : base(proto.access)
    {
        throws = [..proto.throws];
        arguments = [..proto.args.Select(CodeArgument.FromTuple)];
    }

    protected CodeModel(MemberAccess access, ImmutableArray<string> throws, ImmutableArray<CodeArgument> arguments) : base(access)
    {
        this.throws = throws;
        this.arguments = arguments;
    }
}