namespace MahoBootstrap.Prototypes;

public interface ICodePrototype
{
    public List<(string type, string name)> args { get; }
    public List<string> throws { get; }
}