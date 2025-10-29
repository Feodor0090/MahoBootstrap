namespace MahoBootstrap.Models;

public class MethodAnalysisData
{
    public string xmldoc { get; set; } = null!;
    public string javadoc { get; set; } = null!;
    public MethodEffect effect { get; set; }
    public string? alwaysThrows { get; set; }
    public Dictionary<string, bool> nullability { get; set; } = null!;
}

public enum MethodEffect
{
    Empty,
    Pure,
    HasSideEffects
}