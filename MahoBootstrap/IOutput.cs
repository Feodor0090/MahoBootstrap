using System.Collections.Frozen;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap;

public interface IOutput
{
    void Accept(string targetFolder, FrozenDictionary<string, ClassPrototype> prototypes);
}