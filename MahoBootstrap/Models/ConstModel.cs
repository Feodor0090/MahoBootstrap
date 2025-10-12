using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public class ConstModel : DataModel
{
    public readonly Type dotnetType;

    public string? constantValue;

    public ConstModel(FieldPrototype fp) : base(fp)
    {
        dotnetType = GetConstType(fp) ?? throw new ArgumentException();
    }

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
                case "java.lang.String":
                    return typeof(string);
                default:
                    return null;
            }
        }

        return null;
    }
}