namespace OracleDepsSol.Models;

/// <summary>
/// Represents a dependency graph of Oracle database objects
/// </summary>
public sealed class DepGraph
{
    /// <summary>
    /// All discovered tables
    /// </summary>
    public HashSet<string> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All discovered views
    /// </summary>
    public HashSet<string> Views { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All discovered database objects (tables, views, packages, procedures, functions, triggers)
    /// </summary>
    public HashSet<DbObjectId> Objects { get; } = new();

    /// <summary>
    /// All dependency edges between objects
    /// </summary>
    public HashSet<DepEdge> Edges { get; } = new();

    /// <summary>
    /// Gets only table-to-table foreign key relationships
    /// </summary>
    public IEnumerable<(string fromTable, string toTable)> TableFkEdges()
    {
        foreach (var e in Edges)
            if (e.Kind == DepKind.ForeignKey && e.From.Kind == DbObjectKind.Table)
                yield return (e.From.Name, e.ToTable);
    }

    // ========== GRAPH STATISTICS ==========

    /// <summary>
    /// Get statistics about the dependency graph
    /// </summary>
    public DepGraphStats GetStats()
    {
        var objectsByKind = Objects.GroupBy(o => o.Kind)
      .ToDictionary(g => g.Key, g => g.Count());

        var edgesByKind = Edges.GroupBy(e => e.Kind)
   .ToDictionary(g => g.Key, g => g.Count());

        var allTables = new HashSet<string>(Tables, StringComparer.OrdinalIgnoreCase);
        allTables.UnionWith(Views);

        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in allTables)
        {
            inDegree[table] = Edges.Count(e => string.Equals(e.ToTable, table, StringComparison.OrdinalIgnoreCase));
            outDegree[table] = Edges.Count(e => string.Equals(e.From.Name, table, StringComparison.OrdinalIgnoreCase));
        }

        return new DepGraphStats
        {
            TotalTables = Tables.Count,
            TotalObjects = Objects.Count,
            TotalEdges = Edges.Count,
            ObjectsByKind = objectsByKind,
            EdgesByKind = edgesByKind,
            InDegree = inDegree,
            OutDegree = outDegree,
            TotalObjectsByKind = objectsByKind.Values.Sum()
        };
    }

    // ========== ANALYSIS METHODS ==========

    /// <summary>
    /// Find all tables that depend on a specific table (directly or indirectly)
    /// </summary>
    public HashSet<string> FindDependentTables(string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        queue.Enqueue(tableName);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            // Find all edges where 'current' is the source
            var dependents = Edges
          .Where(e => string.Equals(e.From.Name, current, StringComparison.OrdinalIgnoreCase) && e.Kind == DepKind.ForeignKey)
     .Select(e => e.ToTable)
     .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var dep in dependents)
            {
                if (!visited.Contains(dep))
                {
                    result.Add(dep);
                    queue.Enqueue(dep);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Find all tables that a specific table depends on (directly or indirectly)
    /// </summary>
    public HashSet<string> FindDependencies(string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        queue.Enqueue(tableName);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            // Find all edges where 'current' is the target
            var dependencies = Edges
       .Where(e => string.Equals(e.ToTable, current, StringComparison.OrdinalIgnoreCase) && e.Kind == DepKind.ForeignKey)
       .Select(e => e.From.Name)
     .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var dep in dependencies)
            {
                if (!visited.Contains(dep))
                {
                    result.Add(dep);
                    queue.Enqueue(dep);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Find all objects that directly or indirectly use a specific table
    /// </summary>
    public HashSet<DbObjectId> FindObjectsUsingTable(string tableName)
    {
        var result = new HashSet<DbObjectId>();

        var usingEdges = Edges.Where(e => string.Equals(e.ToTable, tableName, StringComparison.OrdinalIgnoreCase));

        foreach (var edge in usingEdges)
        {
            result.Add(edge.From);

            // Find objects that depend on these objects
            var dependentObjects = FindObjectsDependingOn(edge.From);
            foreach (var obj in dependentObjects)
                result.Add(obj);
        }

        return result;
    }

    /// <summary>
    /// Find all objects that depend on a specific object
    /// </summary>
    private HashSet<DbObjectId> FindObjectsDependingOn(DbObjectId obj)
    {
        var result = new HashSet<DbObjectId>();
        var visited = new HashSet<DbObjectId>();
        var queue = new Queue<DbObjectId>();

        queue.Enqueue(obj);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            var dependents = Edges
       .Where(e => string.Equals(e.ToTable, current.Name, StringComparison.OrdinalIgnoreCase))
     .Select(e => e.From)
         .Distinct();

            foreach (var dependent in dependents)
            {
                if (!visited.Contains(dependent))
                {
                    result.Add(dependent);
                    queue.Enqueue(dependent);
                }
            }
        }

        return result;
    }

    // ========== OBJECT QUERY METHODS ==========

    /// <summary>
    /// Get all objects of a specific kind
    /// </summary>
    public IEnumerable<DbObjectId> GetObjectsByKind(DbObjectKind kind)
    {
        return Objects.Where(o => o.Kind == kind);
    }

    /// <summary>
    /// Get all edges of a specific kind
    /// </summary>
    public IEnumerable<DepEdge> GetEdgesByKind(DepKind kind)
    {
        return Edges.Where(e => e.Kind == kind);
    }

    /// <summary>
    /// Get all objects that reference a specific table
    /// </summary>
    public IEnumerable<DbObjectId> GetObjectsReferencingTable(string tableName)
    {
        return Edges
    .Where(e => string.Equals(e.ToTable, tableName, StringComparison.OrdinalIgnoreCase))
  .Select(e => e.From)
  .Distinct();
    }

    /// <summary>
    /// Get all tables referenced by a specific object
    /// </summary>
    public IEnumerable<string> GetTablesReferencedByObject(string objectName)
    {
        return Edges
                   .Where(e => string.Equals(e.From.Name, objectName, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ToTable)
              .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // ========== DATA POPULATION HELPERS ==========

    /// <summary>
    /// Add a table to the graph
    /// </summary>
    public void AddTable(string tableName)
    {
        if (!string.IsNullOrWhiteSpace(tableName))
            Tables.Add(tableName);
    }

    /// <summary>
    /// Add a view to the graph
    /// </summary>
    public void AddView(string viewName)
    {
        if (!string.IsNullOrWhiteSpace(viewName))
            Views.Add(viewName);
    }

    /// <summary>
    /// Add an object to the graph
    /// </summary>
    public void AddObject(DbObjectId obj)
    {
        if (obj != null)
            Objects.Add(obj);
    }

    /// <summary>
    /// Add a dependency edge to the graph
    /// </summary>
    public void AddEdge(DbObjectId from, string toTable, DepKind kind)
    {
        if (from != null && !string.IsNullOrWhiteSpace(toTable))
        {
            Edges.Add(new DepEdge(from, toTable, kind));
            AddTable(toTable);
        }
    }

    /// <summary>
    /// Merge another graph into this one
    /// </summary>
    public void Merge(DepGraph other)
    {
        if (other == null)
            return;

        foreach (var table in other.Tables)
            AddTable(table);

        foreach (var view in other.Views)
            AddView(view);

        foreach (var obj in other.Objects)
            AddObject(obj);

        foreach (var edge in other.Edges)
            Edges.Add(edge);
    }

    /// <summary>
    /// Clear all data
    /// </summary>
    public void Clear()
    {
        Tables.Clear();
        Views.Clear();
        Objects.Clear();
        Edges.Clear();
    }

    /// <summary>
    /// Get a summary of the graph
    /// </summary>
    public override string ToString()
    {
        var stats = GetStats();
        return $"DepGraph: {stats.TotalTables} tables, {stats.TotalObjects} objects, {stats.TotalEdges} edges";
    }
}
