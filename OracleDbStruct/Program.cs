using OracleDbStruct.Models;
using OracleDbStruct.Services;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: OracleDbStruct <ddl-file-path>");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Input file not found: {path}");
            return 1;
        }

        string ddlText;
        try
        {
            ddlText = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read input file: {ex.Message}");
            return 1;
        }

        var analyzer = new DdlDependencyAnalyzer();
        IReadOnlyCollection<DatabaseObject> objects;
        try
        {
            objects = analyzer.Analyze(ddlText);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to analyze DDL: {ex.Message}");
            return 1;
        }

        if (objects.Count == 0)
        {
            Console.WriteLine("No CREATE statements were found in the provided DDL script.");
            return 0;
        }

        Console.WriteLine("Database object dependencies:");
        foreach (var dbObject in objects)
        {
            Console.WriteLine($"- {dbObject}");
            if (dbObject.TableDependencies.Count == 0)
            {
                Console.WriteLine("  Depends on: (no tables)");
            }
            else
            {
                foreach (var dependency in dbObject.TableDependencies)
                {
                    Console.WriteLine($"  Depends on: {dependency}");
                }
            }
        }

        var tableUsage = objects
            .SelectMany(obj => obj.TableDependencies.Select(table => (Table: table, Object: obj)))
            .GroupBy(x => x.Table, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tableUsage.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Tables referenced by other objects:");
            foreach (var group in tableUsage)
            {
                var referencing = string.Join(", ", group.Select(item => $"{item.Object.Name} [{item.Object.Type}]").Distinct(StringComparer.OrdinalIgnoreCase));
                Console.WriteLine($"- {group.Key}: {referencing}");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("No table dependencies were detected.");
        }

        return 0;
    }
}