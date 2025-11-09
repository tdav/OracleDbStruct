namespace OracleDepsSol.Models;

public sealed record DbObjectId(string Name, DbObjectKind Kind)
{
    public override string ToString() => $"{Kind}:{Name}";
}
