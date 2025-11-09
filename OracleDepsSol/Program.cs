using System.Text.Json;
using OracleDepsSol.Models;
using OracleDepsSol.Serivices;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var ddlPath = @"c:\Works_Java\schema_all_asbt.txt";
            var ddl = await File.ReadAllTextAsync(ddlPath);

            Console.WriteLine($"Анализ начал в: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

            // Используем параллельный асинхронный анализатор
            var graph = await OracleDependencyAnalyzer.AnalyzeParallelAsync(ddl);

            Console.WriteLine($"Анализ завершен в: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

            // Вывести основную сводку
            // ReportService.PrintSummary(graph);

            // Вывести неиспользуемые таблицы
            ReportService.PrintUnusedTables(graph);

            // Экспорт по ключам CLI
            string? dotOut = null, jsonOut = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--dot" && i + 1 < args.Length) dotOut = args[++i];
                if (args[i] == "--json" && i + 1 < args.Length) jsonOut = args[++i];
            }

            if (dotOut is not null)
            {
                var dot = ReportService.BuildDot(graph);
                await File.WriteAllTextAsync(dotOut, dot);
                Console.WriteLine($"\nDOT written: {dotOut}");
            }

            if (jsonOut is not null)
            {
                var json = ReportService.BuildJson(graph);
                await File.WriteAllTextAsync(jsonOut, json);
                Console.WriteLine($"JSON written: {jsonOut}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }
}