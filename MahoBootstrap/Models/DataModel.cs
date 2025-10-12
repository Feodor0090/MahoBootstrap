using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public abstract class DataModel : ModelBase, IEquatable<DataModel>
{
    public readonly MemberType type;
    public readonly string name;
    public readonly string fieldType;

    protected DataModel(FieldPrototype fp) : base(fp.access)
    {
        type = fp.memberType;
        name = fp.name;
        fieldType = fp.fieldType;
    }

    public bool Equals(DataModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return type == other.type && name == other.name && fieldType == other.fieldType;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((DataModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)type, name, fieldType);
    }

    public static bool operator ==(DataModel? left, DataModel? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DataModel? left, DataModel? right)
    {
        return !Equals(left, right);
    }
}