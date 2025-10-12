using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class ModelBase
{
    public readonly MemberAccess Access;

    public ModelBase(MemberAccess access)
    {
        Access = access;
    }
}