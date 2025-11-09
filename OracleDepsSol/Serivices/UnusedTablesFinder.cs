using OracleDepsSol.Models;

namespace OracleDepsSol.Serivices;

public static class UnusedTablesFinder
{
    /// <summary>
    /// Находит таблицы, которые:
    /// - Не используются в других таблицах (как Referenced в FK)
    /// - Не используются в Views, PLSQL объектах (SELECT, INSERT, UPDATE, DELETE)
    /// </summary>
    public static HashSet<string> FindUnused(DepGraph graph)
    {
        // Все таблицы
      var allTables = new HashSet<string>(graph.Tables, StringComparer.OrdinalIgnoreCase);
    
        // Таблицы, на которые ссылаются (используются)
        var usedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

     // 1) Таблицы, на которые ссылаются другие таблицы через FK
        foreach (var edge in graph.Edges)
        {
       if (edge.Kind == DepKind.ForeignKey)
   {
         usedTables.Add(edge.ToTable);
        }
        }

        // 2) Таблицы, используемые в Views и PLSQL объектах (SELECT, INSERT, UPDATE, DELETE)
      foreach (var edge in graph.Edges)
 {
     if (edge.Kind is DepKind.ViewQuery or DepKind.DmlRead or DepKind.DmlWrite)
        {
         usedTables.Add(edge.ToTable);
   }
     }

        // 3) Таблицы, которые сами являются объектами (исходные таблицы, даже если они не используются)
        // они не считаются неиспользованными, так как сама их определение — факт существования
        // но если нужна более строгая логика — можно оставить как есть

        // Возвращаем только таблицы, которых НЕТ в списке используемых
        var unused = allTables.Where(t => !usedTables.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        return unused;
    }
}
