namespace MahoBootstrap.Prototypes;

public interface ICodePrototype
{
    public MemberAccess access { get; }
    public List<(string type, string name)> args { get; }
    public List<string> throws { get; }
}