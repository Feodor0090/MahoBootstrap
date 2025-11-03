namespace MahoBootstrap;

public interface ILLMJob
{
    void Run();

    int inputHash { get; }

    string queryId { get; }
}