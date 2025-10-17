using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class ConstModel : DataModel, IEquatable<ConstModel>
{
    public readonly Type dotnetType;

    public string? constantValue;

    public ConstModel(FieldPrototype fp) : base(fp)
    {
        dotnetType = GetConstType(fp) ?? throw new ArgumentException();
    }

    public override string ToString() => $"const {fieldType} {name} = {constantValue}";
    public bool Equals(ConstModel? other) => Equals((DataModel?)other);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is ConstModel other && Equals(other);
    public override int GetHashCode() => base.GetHashCode();
    public static bool operator ==(ConstModel? left, ConstModel? right) => Equals(left, right);
    public static bool operator !=(ConstModel? left, ConstModel? right) => !Equals(left, right);

    public static Type? GetConstType(FieldPrototype fp)
    {
        if (fp.memberType == (MemberType.Final | MemberType.Static))
        {
            switch (fp.fieldType)
            {
                case "double":
                    return typeof(double);
                case "float":
                    return typeof(float);
                case "long":
                    return typeof(long);
                case "int":
                    return typeof(int);
                case "short":
                    return typeof(short);
                case "byte":
                    return typeof(sbyte);
                case "boolean":
                    return typeof(bool);
                case "char":
                    return typeof(char);
                case "java.lang.String":
                    return typeof(string);
                default:
                    return null;
            }
        }

        return null;
    }

    public static string GetDefaultValue(string type, bool typed)
    {
        switch (type)
        {
            case "double":
                return "0d";
            case "float":
                return "0f";
            case "long":
                return "0L";
            case "int":
                return typed ? "(int)0" : "0";
            case "short":
                return typed ? "(short)0" : "0";
            case "byte":
                return typed ? "(byte)0" : "0";
            case "boolean":
                return "false";
            case "char":
                return "(char)0";
            case "java.lang.String":
                return typed ? "\"\"" : "null";
            default:
                return typed ? $"({type})null" : "null";
        }
    }
}