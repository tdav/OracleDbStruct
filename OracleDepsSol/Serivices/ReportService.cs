using System.Text;
using OracleDepsSol.Models;

namespace OracleDepsSol.Serivices;

/// <summary>
/// Сервис для формирования и вывода отчётов по графу зависимостей
/// </summary>
public static class ReportService
{
    /// <summary>
    /// Выводит основную сводку в консоль
    /// </summary>
    public static void PrintSummary(DepGraph graph)
    {
        // 1) Короткая сводка
        Console.WriteLine("== TABLES (discovered) ==");
        foreach (var t in graph.Tables.OrderBy(_ => _))
            Console.WriteLine($"  {t}");

        Console.WriteLine("\n== FK Dependencies (TABLE -> TABLE) ==");
        foreach (var (fromTable, toTable) in graph.TableFkEdges().OrderBy(e => e.fromTable).ThenBy(e => e.toTable))
            Console.WriteLine($"  {fromTable} -> {toTable}");

        // 2) Все ссылки (VIEW/PLSQL тоже)
        Console.WriteLine("\n== All object->table edges ==");
        foreach (var e in graph.Edges.OrderBy(e => e.From.Name))
            Console.WriteLine($"  {e.From} --[{e.Kind}]--> {e.ToTable}");
    }

    /// <summary>
    /// Выводит неиспользуемые таблицы в консоль
    /// </summary>
    public static void PrintUnusedTables(DepGraph graph)
    {
        var unused = UnusedTablesFinder.FindUnused(graph);

        if (unused.Count == 0)
        {
            Console.WriteLine("\n== UNUSED TABLES ==");
            Console.WriteLine("  (none)");
        }
        else
        {
            Console.WriteLine("\n== UNUSED TABLES ==");
            foreach (var t in unused.OrderBy(_ => _))
                Console.WriteLine($"  {t}");
        }
    }

    /// <summary>
    /// Строит DOT граф для визуализации (Graphviz)
    /// </summary>
    public static string BuildDot(DepGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph deps {");
        sb.AppendLine("  rankdir=LR;");

        // Табличные узлы
        foreach (var t in graph.Tables)
            sb.AppendLine($"  \"TABLE:{t}\" [shape=box];");

        // Объекты (view/plsql)
        foreach (var o in graph.Objects.Where(o => o.Kind != DbObjectKind.Table))
            sb.AppendLine($"  \"{o}\" [shape=ellipse, style=dashed];");

        foreach (var e in graph.Edges)
            sb.AppendLine($"  \"{e.From}\" -> \"TABLE:{e.ToTable}\" [label=\"{e.Kind}\"];");

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Экспортирует граф в JSON формате
    /// </summary>
    public static string BuildJson(DepGraph graph)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            tables = graph.Tables.OrderBy(_ => _).ToArray(),
            edges = graph.Edges.Select(e => new
            {
                from = e.From.ToString(),
                to = e.ToTable,
                kind = e.Kind.ToString()
            })
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        return json;
    }
}
