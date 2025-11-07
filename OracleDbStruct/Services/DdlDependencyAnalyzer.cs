using Antlr4.Runtime;
using System.Text;
using OracleDbStruct.Antlr;
using OracleDbStruct.Models;

namespace OracleDbStruct.Services;

public sealed class DdlDependencyAnalyzer
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public IReadOnlyCollection<DatabaseObject> Analyze(string ddlScript)
    {
        if (string.IsNullOrWhiteSpace(ddlScript))
        {
            return Array.Empty<DatabaseObject>();
        }

        var inputStream = new AntlrInputStream(ddlScript);
        var lexer = new OracleDdlLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();
        var allTokens = tokenStream.GetTokens()
            .Where(t => t.Type != TokenConstants.EOF && t.Channel == TokenConstants.DefaultChannel)
            .ToList();

        var objects = new List<DatabaseObject>();
        var index = 0;
        while (index < allTokens.Count)
        {
            var token = allTokens[index];
            if (token.Type == OracleDdlLexer.CREATE)
            {
                var databaseObject = ParseCreateStatement(allTokens, ref index);
                if (databaseObject is not null)
                {
                    objects.Add(databaseObject);
                }
            }
            else
            {
                index++;
            }
        }

        return objects;
    }

    private static DatabaseObject? ParseCreateStatement(IReadOnlyList<IToken> tokens, ref int index)
    {
        var statementTokens = new List<IToken>();
        var parenDepth = 0;
        var beginEndDepth = 0;
        string? objectType = null;
        string? objectName = null;
        int bodyStartIndex = -1;

        for (var i = index; i < tokens.Count; i++)
        {
            var token = tokens[i];
            statementTokens.Add(token);

            if (token.Type == OracleDdlLexer.LPAREN)
            {
                parenDepth++;
            }
            else if (token.Type == OracleDdlLexer.RPAREN && parenDepth > 0)
            {
                parenDepth--;
            }

            if (objectType is null)
            {
                (objectType, objectName, bodyStartIndex) = ReadObjectHeader(statementTokens);
            }

            if (objectType is not null && token.Type == OracleDdlLexer.BEGIN)
            {
                beginEndDepth++;
            }
            else if (objectType is not null && token.Type == OracleDdlLexer.END && beginEndDepth > 0)
            {
                beginEndDepth--;
            }

            if (token.Type == OracleDdlLexer.SEMI)
            {
                if (objectType is null)
                {
                    index = i + 1;
                    return null;
                }

                var shouldStop = objectType switch
                {
                    "PACKAGE" => parenDepth == 0 && ContainsTokenType(statementTokens, OracleDdlLexer.END),
                    "PACKAGE BODY" => parenDepth == 0 && beginEndDepth == 0,
                    "PROCEDURE" => parenDepth == 0 && beginEndDepth == 0,
                    "FUNCTION" => parenDepth == 0 && beginEndDepth == 0,
                    "TRIGGER" => parenDepth == 0 && beginEndDepth == 0,
                    _ => parenDepth == 0 && beginEndDepth == 0,
                };

                if (shouldStop)
                {
                    index = i + 1;
                    return BuildDatabaseObject(objectType, objectName, statementTokens, bodyStartIndex);
                }
            }
        }

        index = tokens.Count;
        if (objectType is null)
        {
            return null;
        }

        return BuildDatabaseObject(objectType, objectName, statementTokens, bodyStartIndex);
    }

    private static DatabaseObject? BuildDatabaseObject(string objectType, string? objectName, IReadOnlyList<IToken> statementTokens, int bodyStartIndex)
    {
        if (objectName is null)
        {
            return null;
        }

        var dependencies = objectType switch
        {
            "TABLE" => CollectTableDependencies(statementTokens, bodyStartIndex),
            "VIEW" => CollectSelectDependencies(statementTokens, bodyStartIndex),
            "MATERIALIZED VIEW" => CollectSelectDependencies(statementTokens, bodyStartIndex),
            "PACKAGE" => CollectProgramUnitDependencies(statementTokens, bodyStartIndex),
            "PACKAGE BODY" => CollectProgramUnitDependencies(statementTokens, bodyStartIndex),
            "PROCEDURE" => CollectProgramUnitDependencies(statementTokens, bodyStartIndex),
            "FUNCTION" => CollectProgramUnitDependencies(statementTokens, bodyStartIndex),
            "TRIGGER" => CollectProgramUnitDependencies(statementTokens, bodyStartIndex),
            _ => new HashSet<string>(NameComparer)
        };

        var orderedDependencies = dependencies.OrderBy(x => x, NameComparer).ToArray();
        return new DatabaseObject(objectName, objectType, orderedDependencies);
    }

    private static HashSet<string> CollectTableDependencies(IReadOnlyList<IToken> tokens, int startIndex)
    {
        var dependencies = new HashSet<string>(NameComparer);
        var asIndex = FindToken(tokens, OracleDdlLexer.AS, startIndex);
        if (asIndex >= 0)
        {
            var selectDependencies = CollectTableReferences(tokens, asIndex + 1);
            dependencies.UnionWith(selectDependencies);
        }

        for (var i = Math.Max(0, startIndex); i < tokens.Count; i++)
        {
            if (tokens[i].Type == OracleDdlLexer.REFERENCES)
            {
                var nextIndex = i + 1;
                var referencedName = TryReadTableReference(tokens, ref nextIndex);
                if (!string.IsNullOrEmpty(referencedName))
                {
                    dependencies.Add(referencedName);
                }

                i = nextIndex - 1;
            }
        }

        return dependencies;
    }

    private static HashSet<string> CollectSelectDependencies(IReadOnlyList<IToken> tokens, int startIndex)
    {
        var selectStart = FindToken(tokens, OracleDdlLexer.SELECT, startIndex);
        if (selectStart < 0)
        {
            return new HashSet<string>(NameComparer);
        }

        return CollectTableReferences(tokens, selectStart);
    }

    private static HashSet<string> CollectProgramUnitDependencies(IReadOnlyList<IToken> tokens, int startIndex)
    {
        var bodyIndex = FindBodyStart(tokens, startIndex);
        return CollectTableReferences(tokens, bodyIndex);
    }

    private static int FindBodyStart(IReadOnlyList<IToken> tokens, int startIndex)
    {
        for (var i = Math.Max(0, startIndex); i < tokens.Count; i++)
        {
            if (tokens[i].Type == OracleDdlLexer.AS || tokens[i].Type == OracleDdlLexer.IS)
            {
                return i + 1;
            }
        }

        return Math.Max(0, startIndex);
    }

    private static (string? ObjectType, string? ObjectName, int BodyStartIndex) ReadObjectHeader(IReadOnlyList<IToken> tokens)
    {
        if (tokens.Count == 0 || tokens[0].Type != OracleDdlLexer.CREATE)
        {
            return (null, null, -1);
        }

        var index = 1;
        while (index < tokens.Count && (tokens[index].Type == OracleDdlLexer.OR || tokens[index].Type == OracleDdlLexer.REPLACE))
        {
            index++;
        }

        while (index < tokens.Count)
        {
            var token = tokens[index];
            switch (token.Type)
            {
                case OracleDdlLexer.TABLE:
                    {
                        index++;
                        var name = ReadQualifiedName(tokens, ref index);
                        return ("TABLE", name, index);
                    }
                case OracleDdlLexer.MATERIALIZED:
                    {
                        index++;
                        if (index < tokens.Count && tokens[index].Type == OracleDdlLexer.VIEW)
                        {
                            index++;
                        }

                        var name = ReadQualifiedName(tokens, ref index);
                        return ("MATERIALIZED VIEW", name, index);
                    }
                case OracleDdlLexer.VIEW:
                    {
                        index++;
                        var name = ReadQualifiedName(tokens, ref index);
                        return ("VIEW", name, index);
                    }
                case OracleDdlLexer.PACKAGE:
                    {
                        index++;
                        var type = "PACKAGE";
                        if (index < tokens.Count && tokens[index].Type == OracleDdlLexer.BODY)
                        {
                            type = "PACKAGE BODY";
                            index++;
                        }

                        var name = ReadQualifiedName(tokens, ref index);
                        return (type, name, index);
                    }
                case OracleDdlLexer.FUNCTION:
                    {
                        index++;
                        var name = ReadQualifiedName(tokens, ref index);
                        return ("FUNCTION", name, index);
                    }
                case OracleDdlLexer.PROCEDURE:
                    {
                        index++;
                        var name = ReadQualifiedName(tokens, ref index);
                        return ("PROCEDURE", name, index);
                    }
                case OracleDdlLexer.TRIGGER:
                    {
                        index++;
                        var name = ReadQualifiedName(tokens, ref index);
                        return ("TRIGGER", name, index);
                    }
                default:
                    index++;
                    break;
            }
        }

        return (null, null, -1);
    }

    private static HashSet<string> CollectTableReferences(IReadOnlyList<IToken> tokens, int startIndex)
    {
        var references = new HashSet<string>(NameComparer);
        for (var i = Math.Max(0, startIndex); i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Type)
            {
                case OracleDdlLexer.FROM:
                case OracleDdlLexer.JOIN:
                case OracleDdlLexer.INTO:
                case OracleDdlLexer.USING:
                    {
                        var nextIndex = i + 1;
                        var tableName = TryReadTableReference(tokens, ref nextIndex);
                        if (!string.IsNullOrEmpty(tableName))
                        {
                            references.Add(tableName);
                        }

                        i = nextIndex - 1;
                        break;
                    }
                case OracleDdlLexer.UPDATE:
                    {
                        var nextIndex = i + 1;
                        var tableName = TryReadTableReference(tokens, ref nextIndex);
                        if (!string.IsNullOrEmpty(tableName))
                        {
                            references.Add(tableName);
                        }

                        i = nextIndex - 1;
                        break;
                    }
                case OracleDdlLexer.DELETE:
                    {
                        var nextIndex = i + 1;
                        if (nextIndex < tokens.Count && tokens[nextIndex].Type == OracleDdlLexer.FROM)
                        {
                            nextIndex++;
                        }

                        var tableName = TryReadTableReference(tokens, ref nextIndex);
                        if (!string.IsNullOrEmpty(tableName))
                        {
                            references.Add(tableName);
                        }

                        i = nextIndex - 1;
                        break;
                    }
                case OracleDdlLexer.MERGE:
                    {
                        var nextIndex = i + 1;
                        while (nextIndex < tokens.Count && tokens[nextIndex].Type != OracleDdlLexer.SEMI)
                        {
                            if (tokens[nextIndex].Type == OracleDdlLexer.INTO || tokens[nextIndex].Type == OracleDdlLexer.USING)
                            {
                                var afterKeyword = nextIndex + 1;
                                var tableName = TryReadTableReference(tokens, ref afterKeyword);
                                if (!string.IsNullOrEmpty(tableName))
                                {
                                    references.Add(tableName);
                                }

                                nextIndex = afterKeyword;
                            }
                            else if (tokens[nextIndex].Type == OracleDdlLexer.LPAREN)
                            {
                                nextIndex = SkipParentheses(tokens, nextIndex);
                            }
                            else
                            {
                                nextIndex++;
                            }
                        }

                        i = nextIndex;
                        break;
                    }
            }
        }

        return references;
    }

    private static string? TryReadTableReference(IReadOnlyList<IToken> tokens, ref int index)
    {
        while (index < tokens.Count)
        {
            var token = tokens[index];
            if (token.Type == OracleDdlLexer.LPAREN)
            {
                var nextIndex = index + 1;
                if (nextIndex < tokens.Count && tokens[nextIndex].Type == OracleDdlLexer.SELECT)
                {
                    index = SkipParentheses(tokens, index);
                    continue;
                }

                index++;
                continue;
            }

            if (token.Type == OracleDdlLexer.ONLY)
            {
                index++;
                continue;
            }

            if (IsIdentifier(token.Type))
            {
                return ReadQualifiedName(tokens, ref index);
            }

            if (token.Type == OracleDdlLexer.STRING || token.Type == OracleDdlLexer.NUMBER)
            {
                index++;
                continue;
            }

            break;
        }

        return null;
    }

    private static string? ReadQualifiedName(IReadOnlyList<IToken> tokens, ref int index)
    {
        var parts = new List<string>();
        while (index < tokens.Count)
        {
            var token = tokens[index];
            if (IsIdentifier(token.Type))
            {
                parts.Add(NormalizeName(token.Text));
                index++;
                if (index < tokens.Count && tokens[index].Type == OracleDdlLexer.DOT)
                {
                    index++;
                    continue;
                }

                break;
            }

            break;
        }

        return parts.Count == 0 ? null : string.Join('.', parts);
    }

    private static bool IsIdentifier(int tokenType)
        => tokenType == OracleDdlLexer.IDENTIFIER || tokenType == OracleDdlLexer.QUOTED_IDENTIFIER;

    private static string NormalizeName(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (text.StartsWith('"') && text.EndsWith('"'))
        {
            var inner = text.Substring(1, text.Length - 2);
            if (inner.IndexOf('"') >= 0)
            {
                var builder = new System.Text.StringBuilder(inner.Length);
                for (var i = 0; i < inner.Length; i++)
                {
                    if (inner[i] == '"' && i + 1 < inner.Length && inner[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        builder.Append(inner[i]);
                    }
                }

                return builder.ToString();
            }

            return inner;
        }

        return text.ToUpperInvariant();
    }

    private static int FindToken(IReadOnlyList<IToken> tokens, int tokenType, int startIndex)
    {
        for (var i = Math.Max(0, startIndex); i < tokens.Count; i++)
        {
            if (tokens[i].Type == tokenType)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsTokenType(IReadOnlyList<IToken> tokens, int tokenType)
        => tokens.Any(token => token.Type == tokenType);

    private static int SkipParentheses(IReadOnlyList<IToken> tokens, int index)
    {
        if (index >= tokens.Count || tokens[index].Type != OracleDdlLexer.LPAREN)
        {
            return index;
        }

        var depth = 1;
        index++;
        while (index < tokens.Count && depth > 0)
        {
            if (tokens[index].Type == OracleDdlLexer.LPAREN)
            {
                depth++;
            }
            else if (tokens[index].Type == OracleDdlLexer.RPAREN)
            {
                depth--;
            }

            index++;
        }

        return index;
    }
}