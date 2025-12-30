using System.Text.Json;
using OracleDepsSol.Models;
using OracleDepsSol.Serivices;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            //var ddlPath = @"c:\Works_Java\schema_all_asbt.txt";
            //var ddl = await File.ReadAllTextAsync(ddlPath);


            var ddl = @"CREATE OR REPLACE PROCEDURE TEST_PROCEDURE_NAME (
    p_id_input  IN  NUMBER,          -- Входной параметр
    p_status    OUT VARCHAR2         -- Выходной параметр
) IS
    -- Объявление локальных переменных
    v_temp_val  NUMBER;
    v_date      DATE := SYSDATE;
BEGIN
    -- 1. Логика (пример)
    SELECT COUNT(*) 
    INTO v_temp_val 
    FROM DUAL;

    -- 2. Условная логика
    IF p_id_input > 0 THEN
        p_status := 'SUCCESS: ' || v_temp_val;
    ELSE
        p_status := 'INVALID ID';
    END IF;

    -- 3. Фиксация изменений (если были INSERT/UPDATE)
    COMMIT;

EXCEPTION
    -- Обработка ошибок
    WHEN NO_DATA_FOUND THEN
        p_status := 'NOT FOUND';
        ROLLBACK;
    WHEN OTHERS THEN
        p_status := 'ERROR: ' || SQLERRM;
        ROLLBACK;
        -- Логирование ошибки (опционально)
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
END TEST_PROCEDURE_NAME;";


            Console.WriteLine($"Анализ начал в: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

            // Используем параллельный асинхронный анализатор
            var graph =  OracleDependencyAnalyzer.AnalyzeParallelAsync(ddl);

            Console.WriteLine($"Анализ завершен в: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

            // Вывести основную сводку
            ReportService.PrintSummary(graph);

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