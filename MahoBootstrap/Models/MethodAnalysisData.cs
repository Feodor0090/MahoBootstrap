namespace MahoBootstrap.Models;

public class MethodAnalysisData
{
    public string? xmldoc { get; set; }
    public string? javadoc { get; set; }
    public MethodEffect? effect { get; set; }
    public string? alwaysThrows { get; set; }
    public Dictionary<string, bool>? nullability { get; set; }
}

public enum MethodEffect
{
    Empty,
    Pure,
    HasSideEffects
}