using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Antlr4.Runtime.Token;

namespace OracleDeps;

public static class OracleDependencyAnalyzer
{
    public static DepGraph Analyze(string ddl)
    {
        var input = CharStreams.fromString(ddl);
        var upper = new CaseChangingCharStream(input, upper: true);
        var lexer = new PlSqlLexer(upper);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        // (Необязательно, но полезно: построить дерево для валидации)
        try
        {
            var parser = new PlSqlParser(tokens) { BuildParseTrees = true };
            // Корневое правило грамматики
            parser.sql_script();
        }
        catch
        {
            // Продолжаем — аналитика по токенам всё равно даст пользу
        }

        // Соберём только «основные» токены (без комментариев/whitespace)
        var codeTokens = tokens.GetTokens()
            .Where(t => t.Channel == DEFAULT_CHANNEL)
            .Select(t => t.Text)
            .ToList();

        var g = new DepGraph();
        var i = 0;

        while (i < codeTokens.Count)
        {
            string tk = codeTokens[i];

            if (IsKeyword(tk, "CREATE"))
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
                        var obj = new DbObjectId(name, kind);
                        g.Objects.Add(obj);
                        if (kind == DbObjectKind.Table) g.Tables.Add(name);

                        // TABLE: собрать FK → REFERENCES
                        if (kind == DbObjectKind.Table)
                            ExtractFkRefs(codeTokens, j, obj, g);

                        // VIEW: разобрать SELECT … и изъять табличные идентификаторы после FROM/JOIN
                        if (kind == DbObjectKind.View)
                            ExtractQueryTables(codeTokens, j, obj, ref g, DepKind.ViewQuery);

                        // PLSQL объекты: DML-ссылки на таблицы
                        if (kind is DbObjectKind.Package or DbObjectKind.Procedure or DbObjectKind.Function or DbObjectKind.Trigger)
                            ExtractDmlTables(codeTokens, j, obj, g);
                    }
                }

                i = j;
                continue;
            }

            i++;
        }

        return g;
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
        // Считываем [SCHEMA '.'] NAME — до первого символа, означающего начало тела ( '(', 'IS', 'AS', 'AUTHID', 'EDITIONSABLE', ... )
        var sb = new StringBuilder();
        int k = i;

        // имя может быть в кавычках, с dblink (@), с PARTITION/ORGANIZATION — все это отбрасываем до структурных ключевых слов
        while (k < toks.Count)
        {
            var t = toks[k];
            if (t is "(" or "IS" or "AS" or "AUTHID" or "EDITIONABLE" or "NONEDITIONABLE" or "ORGANIZATION" or "EXTERNAL" or "PARTITION" or "CLUSTER" or "OF" or "USING")
                break;

            // имя/точка/@ — собираем
            if (IsIdentPiece(t)) { if (sb.Length > 0) sb.Append(t == "." ? "." : (t == "@" ? "@" : ".")); sb.Append(NormalizeIdent(t)); }
            else if (t == ".") { sb.Append("."); }
            else if (t == "@") { sb.Append("@"); }   // dblink
            else break;

            k++;
        }

        i = k;
        var s = sb.ToString().Trim('.');
        // schema.table@dblink → оставим schema.table, dblink для анализа не нужен
        var at = s.IndexOf('@');
        return at >= 0 ? s[..at] : s;
    }

    private static bool IsIdentPiece(string t)
    {
        if (t == "." || t == "@") return true;
        if (t.StartsWith("\"") && t.EndsWith("\"")) return true;
        // Oracle идентификатор — буквы/цифры/подчерк; оставим просто эвристику
        return char.IsLetterOrDigit(t.FirstOrDefault()) || t == "_";
    }

    private static string NormalizeIdent(string t)
    {
        if (t.StartsWith("\"") && t.EndsWith("\""))
            return t[1..^1]; // убираем кавычки
        return t;
    }

    private static void ExtractFkRefs(List<string> toks, int startIdx, DbObjectId obj, DepGraph g)
    {
        // Ищем шаблоны: FOREIGN KEY ... REFERENCES <name>
        for (int k = startIdx; k < toks.Count; k++)
        {
            if (IsKeyword(toks[k], "CREATE")) break; // следующий объект — выходим

            if (IsKeyword(toks[k], "FOREIGN") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "KEY"))
            {
                // прыгнем к REFERENCES
                int j = k + 2;
                while (j < toks.Count && !(IsKeyword(toks[j], "REFERENCES"))) j++;
                if (j < toks.Count && IsKeyword(toks[j], "REFERENCES"))
                {
                    j++;
                    var refName = ReadObjectName(toks, ref j);
                    if (!string.IsNullOrWhiteSpace(refName))
                    {
                        g.Tables.Add(refName);
                        g.Edges.Add(new DepEdge(obj, refName, DepKind.ForeignKey));
                    }
                }
            }
        }
    }

    private static void ExtractQueryTables(List<string> toks, int startIdx, DbObjectId obj, ref DepGraph g, DepKind kind)
    {
        // Ищем отрезок после AS | AS ( ... ) SELECT ... — упрощённая эвристика: FROM / JOIN <name>
        // Работает и для VIEW, и для SELECT внутри PLSQL (если понадобится).
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
                    g.Tables.Add(name);
                    g.Edges.Add(new DepEdge(obj, name, kind));
                }
            }
        }
    }

    private static void ExtractDmlTables(List<string> toks, int startIdx, DbObjectId obj, DepGraph g)
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
                    g.Tables.Add(name);
                    g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                }
            }
            else if (IsKeyword(toks[k], "UPDATE"))
            {
                int j = k + 1;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    g.Tables.Add(name);
                    g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                }
            }
            else if (IsKeyword(toks[k], "DELETE") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "FROM"))
            {
                int j = k + 2;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    g.Tables.Add(name);
                    g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                }
            }
            else if (IsKeyword(toks[k], "MERGE") && k + 1 < toks.Count && IsKeyword(toks[k + 1], "INTO"))
            {
                int j = k + 2;
                var name = ReadObjectName(toks, ref j);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    g.Tables.Add(name);
                    g.Edges.Add(new DepEdge(obj, name, DepKind.DmlWrite));
                }
            }
            else if (IsKeyword(toks[k], "SELECT"))
            {
                ExtractQueryTables(toks, k, obj, ref g, DepKind.DmlRead);
            }
        }
    }
}
