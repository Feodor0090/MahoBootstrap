using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class DataModel : ModelBase
{
    public readonly MemberType type;
    public readonly string name;
    public readonly string fieldType;

    public DataModel(FieldPrototype fp) : base(fp.access)
    {
        type = fp.memberType;
        name = fp.name;
        fieldType = fp.fieldType;
    }
}