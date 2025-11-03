using MahoBootstrap.Models;

namespace MahoBootstrap.Outputs.MidletSharp;

public static class NameMapper
{
    #region Types

    public static string MapType(string javaType)
    {
        int arrIndex = javaType.IndexOf('[');
        if (arrIndex == -1)
            return MapTypeFiltered(javaType);
        return MapTypeFiltered(javaType[..arrIndex]) + javaType[(arrIndex)..];
    }

    private static string MapTypeFiltered(string javaType)
    {
        switch (javaType)
        {
            case "double":
            case "float":
            case "long":
            case "int":
            case "short":
            case "char":
                return javaType;
            case "byte":
                return "sbyte";
            case "boolean":
                return "bool";
            case "java.lang.String":
                return "string";
            default:
                break;
        }

        if (javaType.StartsWith("java."))
        {
            return MapJaval(javaType);
        }

        if (javaType.StartsWith("javax."))
        {
            return MapJavax(javaType);
        }

        if (javaType.StartsWith("com.nokia."))
        {
            if (javaType.StartsWith("com.nokia.ui."))
                return "MidletSharp.Nokia.UI." + Capitalize(javaType["com.nokia.ui.".Length..]);

            return "MidletSharp.Nokia." + Capitalize(javaType["com.nokia.".Length..]);
        }

        if (javaType.StartsWith("org.w3c."))
            return "MidletSharp.W3C." + Capitalize(javaType["org.w3c.".Length..]);
        if (javaType.StartsWith("javacard."))
            return "MidletSharp.JavaCard." + Capitalize(javaType["javacard.".Length..]);

        return javaType;
    }

    private static string MapJavax(string javaType)
    {
        var split = javaType.Split('.');
        switch (split[1])
        {
            case "bluetooth":
                return "MidletSharp.BT." + Capitalize(split[2..]);
            case "obex":
                return "MidletSharp.OBEX." + Capitalize(split[2..]);
            case "wireless":
                return "MidletSharp.SMS." + Capitalize(split[3..]);
            case "crypto":
                return "MidletSharp.SATSA." + Capitalize(split[2..]);
            case "microedition":
                switch (split[2])
                {
                    case "adpu":
                        return "MidletSharp.SATSA.ADPU" + Capitalize(split[3..]);
                    case "content":
                        return "MidletSharp.CH." + Capitalize(split[3..]);
                    case "io":
                        return "MidletSharp.IO." + Capitalize(split[3..]);
                    case "jcrmi":
                        return "MidletSharp.SATSA.JCRMI" + Capitalize(split[3..]);
                    case "pki":
                        return "MidletSharp.SATSA.PKI" + Capitalize(split[3..]);
                    case "lcdui":
                        return "MidletSharp.UI." + Capitalize(split[3..]);
                    case "location":
                        return "MidletSharp.GPS." + Capitalize(split[3..]);
                    case "m2g":
                        return "MidletSharp.SVG." + Capitalize(split[3..]);
                    case "m3g":
                        return "MidletSharp.M3G." + Capitalize(split[3..]);
                    case "media":
                        return "MidletSharp.MMAPI." + Capitalize(split[3..]);
                    case "midlet":
                        return "MidletSharp." + Capitalize(split[3..]);
                    case "pim":
                        return "MidletSharp.PIM." + Capitalize(split[3..]);
                    case "rms":
                        return "MidletSharp.RMS." + Capitalize(split[3..]);
                    case "securityservice":
                        return "MidletSharp.SATSA.SS." + Capitalize(split[3..]);
                    case "sensor":
                        return "MidletSharp.Sensors." + Capitalize(split[3..]);
                    default:
                        return "MidletSharp." + Capitalize(split[2..]);
                }
        }

        return javaType;
    }

    private static string MapJaval(string javaType)
    {
        var split = javaType.Split('.');
        switch (split[1])
        {
            case "io":
                return "MidletSharp.IO." + Capitalize(split[2..]);
            case "lang":
                return "MidletSharp.Java." + Capitalize(split[2..]);
            case "util":
                return "MidletSharp.Java." + Capitalize(split[2..]);
            case "security":
                return "MidletSharp.SATSA.Security." + Capitalize(split[2..]);
            case "rmi":
                return "MidletSharp.SATSA.RMI." + Capitalize(split[2..]);
            default:
                return "MidletSharp." + Capitalize(split[1..]);
        }
    }

    private static string Capitalize(string javaType)
    {
        return string.Join('.', javaType.Split('.').Select(x => $"{char.ToUpperInvariant(x[0])}{x[1..]}"));
    }

    private static string Capitalize(string[] javaType)
    {
        return string.Join('.', javaType.Select(x => $"{char.ToUpperInvariant(x[0])}{x[1..]}"));
    }

    public static string CutNamespace(string type, string ns)
    {
        if (type.StartsWith(ns))
        {
            if (type[ns.Length] == '.')
            {
                if (type.IndexOf('.', ns.Length + 1) == -1)
                {
                    return type[(ns.Length + 1)..];
                }
            }
        }

        return type;
    }

    #endregion

    #region Bans

    /// <summary>
    /// Determines, does Midlet# constructs the type dynamically from c# type or needs a header.
    /// </summary>
    /// <param name="javaType">Java type name.</param>
    /// <returns>When true, header is not needed.</returns>
    public static bool IsTypeBanned(string javaType)
    {
        switch (javaType)
        {
            case "javax.microedition.midlet.MIDlet":
            case "java.lang.String":
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Names

    public static string MapName(MethodModel model)
    {
        switch (model.methodStyle)
        {
            case MethodStyle.Regular:
                break;
            case MethodStyle.Getter:
                if (model.name.StartsWith("get"))
                    return model.name[3..];
                if (model.name == "size")
                    return "Count";
                break;
            case MethodStyle.Setter:
                if (model.name.StartsWith("set"))
                    return model.name[3..];
                break;
            case MethodStyle.IndexGetter:
                break;
            case MethodStyle.IndexSetter:
                break;
            default:
                break;
        }

        return Capitalize(model.name);
    }

    #endregion
}