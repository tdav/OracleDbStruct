using System.Text.Json;
using OracleDeps;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        //if (args.Length == 0)
        //{
        //    Console.WriteLine("Usage: OracleDeps <ddl.sql> [--dot out.dot] [--json out.json]");
        //    return 1;
        //}

        //var ddlPath = args[0];
        var ddlPath = @"c:\Works_Java\schema_all_asbt.txt";
        var ddl = await File.ReadAllTextAsync(ddlPath);

        var graph = OracleDependencyAnalyzer.Analyze(ddl);

        // 1) Короткая сводка
        Console.WriteLine("== TABLES (discovered) ==");
        foreach (var t in graph.Tables.OrderBy(_ => _)) Console.WriteLine($"  {t}");

        Console.WriteLine("\n== FK Dependencies (TABLE -> TABLE) ==");
        foreach (var (fromTable, toTable) in graph.TableFkEdges().OrderBy(e => e.fromTable).ThenBy(e => e.toTable))
            Console.WriteLine($"  {fromTable} -> {toTable}");

        // 2) Все ссылки (VIEW/PLSQL тоже)
        Console.WriteLine("\n== All object->table edges ==");
        foreach (var e in graph.Edges.OrderBy(e => e.From.Name))
            Console.WriteLine($"  {e.From} --[{e.Kind}]--> {e.ToTable}");

        // 3) Экспорт по ключам CLI
        string? dotOut = null, jsonOut = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--dot" && i + 1 < args.Length) dotOut = args[++i];
            if (args[i] == "--json" && i + 1 < args.Length) jsonOut = args[++i];
        }

        if (dotOut is not null)
        {
            var dot = BuildDot(graph);
            await File.WriteAllTextAsync(dotOut, dot);
            Console.WriteLine($"\nDOT written: {dotOut}");
        }

        if (jsonOut is not null)
        {
            var json = JsonSerializer.Serialize(new
            {
                tables = graph.Tables.OrderBy(_ => _).ToArray(),
                edges = graph.Edges.Select(e => new
                {
                    from = e.From.ToString(),
                    to = e.ToTable,
                    kind = e.Kind.ToString()                    
                })
            }, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(jsonOut, json);
            Console.WriteLine($"JSON written: {jsonOut}");
        }

        return 0;

        // -------- helpers --------

        static string BuildDot(DepGraph g)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("digraph deps {");
            sb.AppendLine("  rankdir=LR;");

            // Табличные узлы
            foreach (var t in g.Tables) sb.AppendLine($"  \"TABLE:{t}\" [shape=box];");

            // Объекты (view/plsql)
            foreach (var o in g.Objects.Where(o => o.Kind != DbObjectKind.Table))
                sb.AppendLine($"  \"{o}\" [shape=ellipse, style=dashed];");

            foreach (var e in g.Edges)
                sb.AppendLine($"  \"{e.From}\" -> \"TABLE:{e.ToTable}\" [label=\"{e.Kind}\"];");

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}