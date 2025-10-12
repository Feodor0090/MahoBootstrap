namespace MahoBootstrap.Prototypes;

public class CtorPrototype : ICodePrototype
{
    public MemberAccess access { get; set; }
    public List<(string type, string name)> args { get; } = new();
    public List<string> throws { get; } = new();
}