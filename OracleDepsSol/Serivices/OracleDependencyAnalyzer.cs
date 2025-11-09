using OracleDepsSol.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace OracleDepsSol.Serivices;

public static class OracleDependencyAnalyzer
{
    /// <summary>
    /// Анализирует Oracle DDL синхронно
    /// </summary>
    public static DepGraph Analyze(string ddl)
    {
        return AnalyzeInternal(ddl, CancellationToken.None, useParallel: false);
    }

    /// <summary>
    /// Анализирует Oracle DDL асинхронно без параллелизма
    /// </summary>
    public static async Task<DepGraph> AnalyzeAsync(string ddl)
    {
        return await AnalyzeAsync(ddl, CancellationToken.None);
    }

    /// <summary>
    /// Анализирует Oracle DDL асинхронно с поддержкой отмены
    /// </summary>
    public static async Task<DepGraph> AnalyzeAsync(string ddl, CancellationToken cancellationToken)
    {
        return await Task.Run(() => AnalyzeInternal(ddl, cancellationToken, useParallel: false), cancellationToken);
    }

    /// <summary>
    /// Анализирует Oracle DDL параллельно
    /// </summary>
    public static DepGraph AnalyzeParallel(string ddl)
    {
        return AnalyzeInternal(ddl, CancellationToken.None, useParallel: true);
    }

    /// <summary>
    /// Анализирует Oracle DDL асинхронно с параллельной обработкой
    /// </summary>
    public static async Task<DepGraph> AnalyzeParallelAsync(string ddl)
    {
        return await AnalyzeParallelAsync(ddl, CancellationToken.None);
    }

    /// <summary>
    /// Анализирует Oracle DDL асинхронно с параллельной обработкой и поддержкой отмены
    /// </summary>
    public static async Task<DepGraph> AnalyzeParallelAsync(string ddl, CancellationToken cancellationToken)
    {
        return await Task.Run(() => AnalyzeInternal(ddl, cancellationToken, useParallel: true), cancellationToken);
    }

    private static DepGraph AnalyzeInternal(string ddl, CancellationToken cancellationToken, bool useParallel)
    {
        var codeTokens = Tokenize(ddl);

        // Находим все CREATE блоки параллельно или последовательно
        var createBlocks = useParallel
            ? FindCreateBlocksParallel(codeTokens, cancellationToken)
            : FindCreateBlocksSequential(codeTokens, cancellationToken);

        var g = new DepGraph();

        if (useParallel)
        {
            // Параллельная обработка CREATE блоков с потокобезопасностью
            Parallel.ForEach(createBlocks, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            (block) =>
            {
                ProcessCreateBlock(codeTokens, block, g);
            });
        }
        else
        {
            // Последовательная обработка
            foreach (var block in createBlocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessCreateBlock(codeTokens, block, g);
            }
        }

        return g;
    }

    private static List<(int start, int end, DbObjectKind kind, string name)> FindCreateBlocksSequential(
        List<string> codeTokens, CancellationToken cancellationToken)
    {
        var blocks = new List<(int, int, DbObjectKind, string)>();
        var i = 0;

        while (i < codeTokens.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsKeyword(codeTokens[i], "CREATE"))
            {
                var j = i + 1;
                if (Match(codeTokens, ref j, "OR", "REPLACE")) { /* optional */ }

                DbObjectKind kind = DbObjectKind.Unknown;
                if (Match(codeTokens, ref j, "TABLE")) kind = DbObjectKind.Table;
                else if (Match(codeTokens, ref j, "VIEW")) kind = DbObjectKind.View;
                else if (Match(codeTokens, ref j, "PACKAGE")) kind = DbObjectKind.Package;
                else if (Match(codeTokens, ref j, "PROCEDURE")) kind = DbObjectKind.Procedure;
                else if (Match(codeTokens, ref j, "FUNCTION")) kind = DbObjectKind.Function;
                else if (Match(codeTokens, ref j, "TRIGGER")) kind = DbObjectKind.Trigger;

                if (kind != DbObjectKind.Unknown)
                {
                    var nameStart = j;
                    var name = ReadObjectName(codeTokens, ref j);

                    if (!string.IsNullOrEmpty(name))
                    {
                        blocks.Add((i, j, kind, name));
                    }
                }

                i = j;
                continue;
            }

            i++;
        }

        return blocks;
    }

    private static List<(int start, int end, DbObjectKind kind, string name)> FindCreateBlocksParallel(
        List<string> codeTokens, CancellationToken cancellationToken)
    {
        var blocks = new ConcurrentBag<(int, int, DbObjectKind, string)>();
        
        // Разбиваем на батчи для параллельной обработки
        const int batchSize = 1000;
        var tokenCount = codeTokens.Count;
        var batches = (int)Math.Ceiling((double)tokenCount / batchSize);

        Parallel.For(0, batches, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        (batchIndex) =>
        {
            var start = batchIndex * batchSize;
            var end = Math.Min(start + batchSize, tokenCount);

            for (int i = start; i < end; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsKeyword(codeTokens[i], "CREATE"))
                {
                    var j = i + 1;
                    if (Match(codeTokens, ref j, "OR", "REPLACE")) { /* optional */ }

                    DbObjectKind kind = DbObjectKind.Unknown;
                    if (Match(codeTokens, ref j, "TABLE")) kind = DbObjectKind.Table;
                    else if (Match(codeTokens, ref j, "VIEW")) kind = DbObjectKind.View;
                    else if (Match(codeTokens, ref j, "PACKAGE")) kind = DbObjectKind.Package;
                    else if (Match(codeTokens, ref j, "PROCEDURE")) kind = DbObjectKind.Procedure;
                    else if (Match(codeTokens, ref j, "FUNCTION")) kind = DbObjectKind.Function;
                    else if (Match(codeTokens, ref j, "TRIGGER")) kind = DbObjectKind.Trigger;

                    if (kind != DbObjectKind.Unknown)
                    {
                        var name = ReadObjectName(codeTokens, ref j);
                        if (!string.IsNullOrEmpty(name))
                        {
                            blocks.Add((i, j, kind, name));
                        }
                    }
                }
            }
        });

        return blocks.OrderBy(b => b.Item1).ToList();
    }

    private static void ProcessCreateBlock(List<string> codeTokens, (int start, int end, DbObjectKind kind, string name) block, DepGraph g)
    {
        var (i, j, kind, name) = block;

        var obj = new DbObjectId(name, kind);
        lock (g.Objects)
        {
            g.Objects.Add(obj);
        }

        if (kind == DbObjectKind.Table)
        {
            lock (g.Tables)
            {
                g.Tables.Add(name);
            }
            ExtractFkRefsThreadSafe(codeTokens, j, obj, g);
        }

        if (kind == DbObjectKind.View)
        {
            ExtractQueryTablesThreadSafe(codeTokens, j, obj, g, DepKind.ViewQuery);
        }

        if (kind is DbObjectKind.Package or DbObjectKind.Procedure or DbObjectKind.Function or DbObjectKind.Trigger)
        {
            ExtractDmlTablesThreadSafe(codeTokens, j, obj, g);
        }
    }

    private static List<string> Tokenize(string ddl)
    {
        var tokens = new List<string>();
        var current = "";

        foreach (var ch in ddl)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current);
                    current = "";
                }
            }
            else if ("(),;.@:='\"".Contains(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current);
                    current = "";
                }
                tokens.Add(ch.ToString());
            }
            else
            {
                current += ch;
            }
        }

        if (current.Length > 0)
            tokens.Add(current);

        return tokens;
    }

    // ---------- helpers ----------

    private static bool IsKeyword(string t, string kw) => string.Equals(t, kw, StringComparison.OrdinalIgnoreCase);

    private static bool Match(List<string> toks, ref int i, params string[] seq)
    {
        int k = i;
        foreach (var s in seq)
        {
            if (k >= toks.Count || !IsKeyword(toks[k], s)) return false;
            k++;
        }
        i = k;
        return true;
    }

    private static string ReadObjectName(List<string> toks, ref int i)
    {
        var sb = new System.Text.StringBuilder();
        int k = i;

        while (k < toks.Count)
        {
            var t = toks[k];
            if (t is "(" or "IS" or "AS" or "AUTHID" or "EDITIONABLE" or "NONEDITIONABLE" or "ORGANIZATION" or "EXTERNAL" or "PARTITION" or "CLUSTER" or "OF" or "USING")
                break;

            if (IsIdentPiece(t)) { if (sb.Length > 0) sb.Append(t == "." ? "." : t == "@" ? "@" : "."); sb.Append(NormalizeIdent(t)); }
            else if (t == ".") { sb.Append("."); }
            else if (t == "@") { sb.Append("@"); }
            else break;

            k++;
        }

        i = k;
        var s = sb.ToString().Trim('.');
        var at = s.IndexOf('@');
        return at >= 0 ? s[..at] : s;
    }

    private static bool IsIdentPiece(string t)
    {
        if (t == "." || t == "@") return true;
        if (t.StartsWith("\"") && t.EndsWith("\"")) return true;
        return char.IsLetterOrDigit(t.FirstOrDefault()) || t == "_";
    }

    private static string NormalizeIdent(string t)
    {
        if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
            return t[1..^1];
        return t;
    }

    private static void ExtractFkRefsThreadSafe(List<string> toks, int startIdx, DbObjectId obj, DepGraph g)
    {
        for (int k = startIdx; k < toks.Count; k++)
        {
            if (IsKeyword(toks[k], "CREATE")) break;

            if (IsKeyword(toks[k], "FOREIGN") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "KEY"))
            {
                int j = k + 2;
                while (j < toks.Count && !IsKeyword(toks[j], "REFERENCES")) j++;
                if (j < toks.Count && IsKeyword(toks[j], "REFERENCES"))
                {
                    j++;
                    var refName = ReadObjectName(toks, ref j);
                    if (!string.IsNullOrWhiteSpace(refName))
                    {
                        lock (g.Tables)
                        {
                            g.Tables.Add(refName);
                        }
                        lock (g.Edges)
                        {
                            g.Edges.Add(new DepEdge(obj, refName, DepKind.ForeignKey));
                        }
                    }
                }
            }
        }
    }

    private static void ExtractQueryTablesThreadSafe(List<string> toks, int startIdx, DbObjectId obj, DepGraph g, DepKind kind)
    {
        bool inSelect = false;
        for (int k = startIdx; k < toks.Count; k++)
        {
            if (IsKeyword(toks[k], "CREATE")) break;

            if (IsKeyword(toks[k], "AS")) { inSelect = true; continue; }
            if (!inSelect && IsKeyword(toks[k], "SELECT")) inSelect = true;

            if (inSelect && (IsKeyword(toks[k], "FROM") || IsKeyword(toks[k], "JOIN") || IsKeyword(toks[k], "UPDATE") || IsKeyword(toks[k], "INTO")))
            {
                int j = k + 1;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (g.Tables)
                    {
                        g.Tables.Add(name);
                    }
                    lock (g.Edges)
                    {
                        g.Edges.Add(new DepEdge(obj, name, kind));
                    }
                }
            }
        }
    }

    private static void ExtractDmlTablesThreadSafe(List<string> toks, int startIdx, DbObjectId obj, DepGraph g)
    {
        for (int k = startIdx; k < toks.Count; k++)
        {
            if (IsKeyword(toks[k], "CREATE")) break;

            if (IsKeyword(toks[k], "INSERT") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "INTO"))
            {
                int j = k + 2;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (g.Tables)
                    {
                        g.Tables.Add(name);
                    }
                    lock (g.Edges)
                    {
                        g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                    }
                }
            }
            else if (IsKeyword(toks[k], "UPDATE"))
            {
                int j = k + 1;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (g.Tables)
                    {
                        g.Tables.Add(name);
                    }
                    lock (g.Edges)
                    {
                        g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                    }
                }
            }
            else if (IsKeyword(toks[k], "DELETE") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "FROM"))
            {
                int j = k + 2;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (g.Tables)
                    {
                        g.Tables.Add(name);
                    }
                    lock (g.Edges)
                    {
                        g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                    }
                }
            }
            else if (IsKeyword(toks[k], "MERGE") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "INTO"))
            {
                int j = k + 2;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (g.Tables)
                    {
                        g.Tables.Add(name);
                    }
                    lock (g.Edges)
                    {
                        g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                    }
                }
            }
            else if (IsKeyword(toks[k], "SELECT"))
            {
                ExtractQueryTablesThreadSafe(toks, k, obj, g, DepKind.DmlRead);
            }
        }
    }
}
