namespace OracleDepsSol.Models;

/// <summary>
/// Statistics about a dependency graph
/// </summary>
public sealed class DepGraphStats
{
    public int TotalTables { get; set; }
    public int TotalObjects { get; set; }
    public int TotalEdges { get; set; }
    public int TotalObjectsByKind { get; set; }
    public Dictionary<DbObjectKind, int> ObjectsByKind { get; set; } = new();
    public Dictionary<DepKind, int> EdgesByKind { get; set; } = new();
    public Dictionary<string, int> InDegree { get; set; } = new();
    public Dictionary<string, int> OutDegree { get; set; } = new();
}
