using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class ModelBase
{
    public readonly MemberAccess access;

    protected ModelBase(MemberAccess access)
    {
        this.access = access;
    }
}