using System.Collections.Frozen;
using MahoBootstrap.Models;

namespace MahoBootstrap;

public interface IOutput
{
    void Accept(string targetFolder, FrozenDictionary<string, ClassModel> models);
}