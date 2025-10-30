namespace MahoBootstrap.Models;

public class ClassAnalysisData
{
    public ListAPI[] listAPI;
    public GroupedEnum[] groupedEnums;
    public string[] keptConsts;
}

public struct ListAPI
{
    public string listType;
    public string? getMethod;
    public string? setMethod;
    public string? addMethod;
    public string? insertMethod;
    public string? removeMethod;
    public string? clearMethod;
    public string? enumMethod;
    public string? countMethod;
}

public struct GroupedEnum
{
    public bool flags;
    public string name;
    public string[] members;
    public string[] usedInMethods;
    public string[] returnedInMethods;
}