namespace MahoBootstrap;

public struct Prompt
{
    public string system;
    public (string, string)[] examples;

    public Prompt(string system, (string, string)[] examples)
    {
        this.system = system;
        this.examples = examples;
    }

    public static implicit operator Prompt(string system)
    {
        return new Prompt(system, []);
    }
}