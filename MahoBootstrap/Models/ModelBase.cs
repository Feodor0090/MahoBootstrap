using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class ModelBase
{
    public readonly MemberAccess access;

    protected ModelBase(MemberAccess access)
    {
        this.access = access;
    }

    public string dotnetAccessMod =>
        access switch
        {
            MemberAccess.Public => "public",
            MemberAccess.Private => "private",
            MemberAccess.Protected => "protected",
            MemberAccess.Package => "internal",
            _ => throw new ArgumentOutOfRangeException()
        };
}