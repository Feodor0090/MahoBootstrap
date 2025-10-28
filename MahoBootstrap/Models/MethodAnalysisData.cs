namespace MahoBootstrap.Models;

public class MethodAnalysisData
{
    public string xmldoc { get; set; } = null!;
    public string javadoc { get; set; } = null!;
    public bool empty { get; set; }
    public string? alwaysThrows { get; set; }

}