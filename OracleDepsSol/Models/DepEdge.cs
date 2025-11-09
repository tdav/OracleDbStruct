namespace OracleDepsSol.Models;

public sealed record DepEdge(DbObjectId From, string ToTable, DepKind Kind);
