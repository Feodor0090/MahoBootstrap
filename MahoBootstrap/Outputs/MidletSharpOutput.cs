using System.Collections.Frozen;
using System.Text;
using MahoBootstrap.Models;
using static MahoBootstrap.Outputs.MidletSharp.NameMapper;

namespace MahoBootstrap.Outputs;

public class MidletSharpOutput : Output
{
    private readonly FrozenDictionary<string, ClassModel> _models;

    public MidletSharpOutput(FrozenDictionary<string, ClassModel> models)
    {
        _models = models;
    }


    public override void Accept(string targetFolder)
    {
        foreach (var model in _models.Values)
        {
            if (IsTypeBanned(model.fullName))
                continue;
            var code = PrintClass(model);
            var name = MapType(model.fullName);
            var dirPath = Path.Combine(targetFolder, Path.Combine(name.Split('.')[..^1]));
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, $"{name.Split('.')[^1]}_mbs.cs"), code);
        }
    }

    private string PrintClass(ClassModel model)
    {
        var ns = string.Join('.', MapType(model.fullName).Split('.')[..^1]);
        var typeName = MapType(model.fullName).Split('.')[^1];

        List<string> lines = new();

        if (model.consts.Length != 0)
        {
            lines.Add("// CONSTANTS\n");
            foreach (var cnst in model.consts)
            {
                lines.Add($"public const {MapType(cnst.fieldType)} {cnst.name} = {cnst.constantValue};\n");
            }
        }

        if (model.ctors.Length != 0)
        {
            lines.Add("// CONSTRUCTORS\n");
            foreach (var ctor in model.ctors)
            {
                lines.Add($"{ctor.dotnetAccessMod} {typeName}({FormatArguments(ctor, ns)})");
                lines.Add("{");
                lines.Add("}\n");
            }
        }

        if (model.fields.Length != 0)
        {
            lines.Add("// FIELDS\n");
            foreach (var f in model.fields)
            {
                lines.Add($"{f.dotnetAccessMod} {f.dotnetFieldType} {MapType(f.fieldType)} @{f.name};\n");
            }
        }

        if (model.methods.Length != 0)
        {
            lines.Add("// METHODS\n");
            List<(MethodModel? @get, MethodModel? @set)> props = new();
            List<MethodModel> regularMethods = new();
            List<MethodModel> allMethods = new(model.methods);
            for (int i = allMethods.Count - 1; i >= 0; i = allMethods.Count - 1)
            {
                MethodModel method = allMethods[i];
                switch (method.MethodStyle)
                {
                    case MethodStyle.Regular:
                    case MethodStyle.IndexGetter:
                    case MethodStyle.IndexSetter:
                    default:
                        regularMethods.Add(method);
                        allMethods.RemoveAt(i);
                        continue;
                    case MethodStyle.Getter:
                        break;
                    case MethodStyle.Setter:
                        break;
                }

                allMethods.RemoveAt(i);
                for (int j = 0; j < allMethods.Count; j++)
                {
                    switch (allMethods[j].MethodStyle)
                    {
                        default:
                            continue;
                        case MethodStyle.Getter:
                        case MethodStyle.Setter:
                        {
                            if (allMethods[j].MethodStyle != method.MethodStyle &&
                                MapName(allMethods[j]) == MapName(method) &&
                                allMethods[j].PropertyType == method.PropertyType)
                            {
                                // found getter+setter
                                if (method.MethodStyle == MethodStyle.Getter)
                                    props.Add((method, allMethods[j]));
                                else
                                    props.Add((allMethods[j], method));
                                allMethods.RemoveAt(j);
                                goto found;
                            }

                            break;
                        }
                    }
                }

                if (method.MethodStyle == MethodStyle.Getter)
                    props.Add((method, null));
                else
                    props.Add((null, method));

                found: ;
            }

            foreach (var (get, set) in props)
            {
                MethodModel refModel = get ?? set!;
                lines.Add(
                    $"{refModel.dotnetAccessMod} {refModel.dotnetMethodType} extern {CutNamespace(MapType(refModel.PropertyType), ns)} {MapName(refModel)} {{");
                if (get != null)
                    lines.Add($"    [MapTo(\"{get.name}\")] get;");
                if (set != null)
                    lines.Add($"    [MapTo(\"{set.name}\")] set;");
                lines.Add("}\n");
            }

            foreach (var m in regularMethods)
            {
                var argsList = FormatArguments(m, ns);
                lines.Add($"[MapTo(\"{m.name}\")]");
                if (model.isInterface)
                {
                    lines.Add($"{CutNamespace(MapType(m.returnType), ns)} {MapName(m)}({argsList});\n");
                }
                else
                {
                    string mt = m.dotnetMethodType;
                    if (mt != "abstract")
                    {
                        mt += " extern";
                    }

                    lines.Add(
                        $"{m.dotnetAccessMod} {mt} {CutNamespace(MapType(m.returnType), ns)} {MapName(m)}({argsList});\n");
                }
            }
        }

        StringBuilder sb = new(
            $"using MidletSharp.Attributes;\n\nnamespace {ns};\n\n" +
            $"[MBSGenerated]\n[JrtClass(\"{model.fullName}\")]\npublic partial {(model.isInterface ? "interface" : "class")} {typeName}");
        if (model.parent != null || model.implements.Length != 0)
        {
            sb.Append(" : ");
            List<string> list = new();
            if (model.parent != null) list.Add(MapType(model.parent));
            foreach (var p in model.implements)
                list.Add(MapType(p));
            sb.Append(string.Join(", ", list));
        }

        sb.Append("\n{");
        foreach (var line in lines)
        {
            sb.Append("\n    ");
            sb.Append(line);
        }

        sb.Append("}\n");

        return sb.ToString();
    }

    private static string FormatArguments(CodeModel m, string ns)
    {
        var argsList = string.Join(", ",
            m.arguments.Select(x => $"{CutNamespace(MapType(x.type), ns)} {x.name}"));
        return argsList;
    }
}