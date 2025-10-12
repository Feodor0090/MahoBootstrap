namespace MahoBootstrap.Models;

public readonly struct CodeArgument : IEquatable<CodeArgument>
{
    public readonly string Type;
    public readonly string Name;

    public CodeArgument(string type, string name)
    {
        Type = type;
        Name = name;
    }

    public static CodeArgument FromTuple((string type, string name) tuple)
    {
        return new CodeArgument(tuple.type, tuple.name);
    }

    public void Deconstruct(out string type, out string name)
    {
        type = Type;
        name = Name;
    }

    public bool Equals(CodeArgument other)
    {
        return Type == other.Type && Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
        return obj is CodeArgument other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
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