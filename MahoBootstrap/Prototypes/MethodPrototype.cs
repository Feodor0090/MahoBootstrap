namespace MahoBootstrap.Prototypes;

public class MethodPrototype : ICodePrototype
{
    public MemberType type;
    public MemberAccess access;
    public string name;
    public string returnType;
    public List<string> throws { get; } = new();
    public List<(string type, string name)> args { get; } = new();
}