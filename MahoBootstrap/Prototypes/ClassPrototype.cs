namespace MahoBootstrap.Prototypes;

public class ClassPrototype
{
    public readonly ClassType type;
    public readonly string pkg;
    public readonly string name;
    public readonly string? parent;
    public readonly List<string> implements = new();
    public readonly List<MethodPrototype> methods = new();
    public readonly List<CtorPrototype> constructors = new();
    public readonly List<FieldPrototype> fields = new();
    public string docText = null!;

    public ClassPrototype(ClassType type, string pkg, string name, string? parent)
    {
        this.type = type;
        this.pkg = pkg;
        this.name = name;
        this.parent = parent;
    }

    public string fullName => string.IsNullOrEmpty(pkg) ? name : $"{pkg}.{name}";
}