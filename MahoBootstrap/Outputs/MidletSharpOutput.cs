using System.Collections.Frozen;
using MahoBootstrap.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MahoBootstrap.Outputs;

public class MidletSharpOutput : Output
{
    private readonly FrozenDictionary<string, ClassModel> models;

    public MidletSharpOutput(FrozenDictionary<string, ClassModel> models)
    {
        this.models = models;
    }

    public override void Accept(string targetFolder)
    {
        foreach (var model in models.Values)
        {
            TypeDeclarationSyntax tds;
            if (model.IsInterface)
            {
                tds = SyntaxFactory.InterfaceDeclaration(model.name);
            }
            else
            {
                tds = SyntaxFactory.ClassDeclaration(model.name);
            }

            var cu = SyntaxFactory.CompilationUnit()
                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("MidletSharp.Attributes")))
                .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(model.pkg)).AddMembers(tds));

            var code = cu.NormalizeWhitespace("    ", Environment.NewLine).ToFullString();
            var dirPath = Path.Combine(targetFolder, Path.Combine(model.pkg.Split('.')));
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, $"{model.name}.cs"), code);
        }
    }
}