using System.Text;
using MahoBootstrap.Models;
using static MahoBootstrap.Outputs.MidletSharp.NameMapper;

namespace MahoBootstrap.Outputs;

public class MidletSharpOutput : Output
{
    public override void Accept(string targetFolder)
    {
        foreach (var model in Program.models.Values)
        {
            if (IsTypeBanned(model.fullName))
                continue;
            var code = ProcessClass(model);
            var name = MapType(model.fullName);
            var dirPath = Path.Combine(targetFolder, Path.Combine(name.Split('.')[..^1]));
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, $"{name.Split('.')[^1]}_mbs.cs"), code);
        }
    }

    private string ProcessClass(ClassModel model)
    {
        var ns = string.Join('.', MapType(model.fullName).Split('.')[..^1]);
        var typeName = MapType(model.fullName).Split('.')[^1];

        List<string> lines = new();

        if (model.consts.Length != 0)
        {
            ProcessConstants(model, lines);
        }

        if (model.ctors.Length != 0)
        {
            ProcessCtors(model, lines, typeName, ns);
        }

        if (model.fields.Length != 0)
        {
            ProcessFields(model, lines);
        }

        if (model.methods.Length != 0)
        {
            ProcessMethods(model, lines, ns);
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

        if (model.analysisData.groupedEnums != null)
        {
            foreach (var group in model.analysisData.groupedEnums)
            {
                if (group.flags)
                    sb.Append("\n[System.Flags]");
                sb.Append($"\npublic enum {group.name} {{\n");
                foreach (var member in group.members)
                    sb.Append($"    {member} = {model.consts.Single(x => x.name == member).constantValue},");
                sb.Append("}\n");
            }
        }

        return sb.ToString();
    }

    private static void ProcessConstants(ClassModel model, List<string> lines)
    {
        lines.Add("// CONSTANTS\n");
        foreach (var cnst in model.consts)
        {
            lines.Add($"public const {MapType(cnst.fieldType)} {cnst.name} = {cnst.constantValue};\n");
        }
    }

    private static void ProcessCtors(ClassModel model, List<string> lines, string typeName, string ns)
    {
        lines.Add("// CONSTRUCTORS\n");
        foreach (var ctor in model.ctors)
        {
            lines.Add($"{ctor.dotnetAccessMod} {typeName}({FormatArguments(ctor, ns)})");
            lines.Add("{");
            lines.Add("}\n");
        }
    }

    private static void ProcessFields(ClassModel model, List<string> lines)
    {
        lines.Add("// FIELDS\n");
        foreach (var f in model.fields)
        {
            lines.Add($"{f.dotnetAccessMod} {f.dotnetFieldType} {MapType(f.fieldType)} @{f.name};\n");
        }
    }

    private static void ProcessMethods(ClassModel model, List<string> lines, string ns)
    {
        lines.Add("// METHODS\n");
        List<(MethodModel? @get, MethodModel? @set)> props = new();
        List<MethodModel> regularMethods = new();
        List<MethodModel> allMethods = new(model.methods);
        for (int i = allMethods.Count - 1; i >= 0; i = allMethods.Count - 1)
        {
            MethodModel method = allMethods[i];
            switch (method.methodStyle)
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
                switch (allMethods[j].methodStyle)
                {
                    default:
                        continue;
                    case MethodStyle.Getter:
                    case MethodStyle.Setter:
                    {
                        if (allMethods[j].methodStyle != method.methodStyle &&
                            MapName(allMethods[j]) == MapName(method) &&
                            allMethods[j].propertyType == method.propertyType)
                        {
                            // found getter+setter
                            if (method.methodStyle == MethodStyle.Getter)
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

            if (method.methodStyle == MethodStyle.Getter)
                props.Add((method, null));
            else
                props.Add((null, method));

            found: ;
        }

        foreach (var (get, set) in props)
        {
            MethodModel refModel = get ?? set!;
            lines.Add(
                $"{refModel.dotnetAccessMod} {refModel.dotnetMethodType} extern {CutNamespace(MapType(refModel.propertyType), ns)} {MapName(refModel)} {{");
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

    private static string FormatArguments(CodeModel m, string ns)
    {
        var argsList = m.arguments.Select(x =>
        {
            string nullableMark;
            if ((m.analysisData.nullability?.TryGetValue(x.name, out var value) ?? false) && value)
                nullableMark = "?";
            else
                nullableMark = "";
            return $"{CutNamespace(MapType(x.type), ns)}{nullableMark} {x.name}";
        });
        return string.Join(", ", argsList);
    }
}