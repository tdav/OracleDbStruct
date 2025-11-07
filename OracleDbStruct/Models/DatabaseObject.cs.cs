namespace OracleDbStruct.Models;

public sealed record DatabaseObject(string Name, string Type, IReadOnlyCollection<string> TableDependencies)
{
    public override string ToString() => $"[{Type}] {Name}";
}