namespace OracleDeps;

public enum DbObjectKind { Table, View, Package, Procedure, Function, Trigger, Unknown }

public sealed record DbObjectId(string Name, DbObjectKind Kind)
{
    public override string ToString() => $"{Kind}:{Name}";
}

public enum DepKind { ForeignKey, ViewQuery, DmlRead, DmlWrite }

public sealed record DepEdge(DbObjectId From, string ToTable, DepKind Kind);

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
