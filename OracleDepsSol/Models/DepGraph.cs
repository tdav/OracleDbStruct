namespace OracleDepsSol.Models;

public sealed class DepGraph
{
    public HashSet<string> Tables { get; } = new();
    public HashSet<DbObjectId> Objects { get; } = new();
    public HashSet<DepEdge> Edges { get; } = new();

    public IEnumerable<(string fromTable, string toTable)> TableFkEdges()
    {
        foreach (var e in Edges)
            if (e.Kind == DepKind.ForeignKey && e.From.Kind == DbObjectKind.Table)
                yield return (e.From.Name, e.ToTable);
    }
}
