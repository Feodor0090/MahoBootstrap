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
}