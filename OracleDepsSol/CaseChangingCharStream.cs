using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;

namespace OracleDeps;

public sealed class CaseChangingCharStream : ICharStream
{
    private readonly ICharStream _stream;
    private readonly bool _upper;

    public CaseChangingCharStream(ICharStream stream, bool upper)
    {
        _stream = stream;
        _upper = upper;
    }

    public string SourceName => _stream.SourceName;
    public int Index => _stream.Index;
    public int Size => _stream.Size;

    public void Consume() => _stream.Consume();
    public int LA(int i)
    {
        int c = _stream.LA(i);
        if (c <= 0) return c;
        char ch = (char)c;
        ch = _upper ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
        return ch;
    }
    public int Mark() => _stream.Mark();
    public void Release(int marker) => _stream.Release(marker);
    public void Seek(int index) => _stream.Seek(index);
    public string GetText(Interval interval) => _stream.GetText(interval);
}
