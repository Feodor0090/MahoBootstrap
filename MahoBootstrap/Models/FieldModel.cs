using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class FieldModel : DataModel, IEquatable<FieldModel>
{
    public FieldModel(FieldPrototype fp) : base(fp)
    {
        if (ConstModel.GetConstType(fp) != null)
            throw new ArgumentException();
    }

    public bool Equals(FieldModel? other) => Equals((DataModel?)other);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is FieldModel other && Equals(other);
    public override int GetHashCode() => base.GetHashCode();
    public static bool operator ==(FieldModel? left, FieldModel? right) => Equals(left, right);
    public static bool operator !=(FieldModel? left, FieldModel? right) => !Equals(left, right);
}