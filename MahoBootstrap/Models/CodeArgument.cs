namespace MahoBootstrap.Models;

public readonly struct CodeArgument : IEquatable<CodeArgument>
{
    public readonly string type;
    public readonly string name;

    public CodeArgument(string type, string name)
    {
        this.type = type;
        this.name = name;
    }

    public static CodeArgument FromTuple((string type, string name) tuple)
    {
        return new CodeArgument(tuple.type, tuple.name);
    }

    public void Deconstruct(out string type, out string name)
    {
        type = this.type;
        name = this.name;
    }

    public bool Equals(CodeArgument other)
    {
        return type == other.type && name == other.name;
    }

    public override bool Equals(object? obj)
    {
        return obj is CodeArgument other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(type, name);
    }

    public static bool operator ==(CodeArgument left, CodeArgument right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CodeArgument left, CodeArgument right)
    {
        return !left.Equals(right);
    }
}