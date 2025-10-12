using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class FieldModel : DataModel
{
    public FieldModel(FieldPrototype fp) : base(fp)
    {
        if (ConstModel.GetConstType(fp) != null)
            throw new ArgumentException();
    }
}