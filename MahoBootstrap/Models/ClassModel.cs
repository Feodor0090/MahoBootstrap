using System.Collections.Immutable;
using MahoBootstrap.Prototypes;

namespace MahoBootstrap.Models;

public sealed class ClassModel : IEquatable<ClassModel>
{
    /// <summary>
    /// Where this class was read? Null if merged/generated.
    /// </summary>
    public readonly string? source;

    public readonly ClassType classType;
    public readonly string pkg;
    public readonly string name;
    public readonly string? parent;
    public readonly ImmutableArray<string> implements;
    public readonly ImmutableArray<CtorModel> ctors;
    public readonly ImmutableArray<MethodModel> methods;
    public readonly ImmutableArray<FieldModel> fields;
    public readonly ImmutableArray<ConstModel> consts;

    /// <summary>
    /// Froze prototype to read-only model.
    /// </summary>
    /// <param name="cp">Prototype</param>
    /// <param name="from">Source of the class</param>
    public ClassModel(ClassPrototype cp, string? from)
    {
        source = from;
        classType = cp.type;
        pkg = cp.pkg;
        name = cp.name;
        parent = cp.parent == "java.lang.Object" ? null : cp.parent;
        implements = [..cp.implements];
        ctors = [..cp.constructors.Select(c => new CtorModel(c))];
        methods =
        [
            ..cp.methods.Select(c =>
            {
                var mm = new MethodModel(c);
                mm.owner = this;
                return mm;
            })
        ];
        fields = [..cp.fields.Where(c => ConstModel.GetConstType(c) == null).Select(c => new FieldModel(c))];
        consts = [..cp.fields.Where(c => ConstModel.GetConstType(c) != null).Select(c => new ConstModel(c))];
    }

    /// <summary>
    /// Merge two models.
    /// </summary>
    /// <param name="class1">Base model.</param>
    /// <param name="class2">Overlay model.</param>
    public ClassModel(ClassModel class1, ClassModel class2)
    {
        source = null;
        if (class1.classType == class2.classType)
        {
            classType = class1.classType;
        }
        else if (class1.classType == ClassType.Interface || class2.classType == ClassType.Interface)
        {
            throw new ArgumentException("Attempt to merge class with interface");
        }
        else if (class1.classType == ClassType.Abstract || class2.classType == ClassType.Abstract)
        {
            throw new ArgumentException("Attempt to merge abstract class with non-abstract class");
        }
        else
        {
            // optimistic: if one api is final while other is not, lets assume implementation made it regular
            classType = ClassType.Regular;
        }

        if (class1.fullName != class2.fullName)
            throw new ArgumentException("Attempt to merge class with different names");
        pkg = class1.pkg;
        name = class1.name;
        if (class1.parent == class2.parent)
        {
            parent = class1.parent;
        }
        else if (class1.parent == null)
        {
            parent = class2.parent;
        }
        else if (class2.parent == null)
        {
            parent = class1.parent;
        }
        else
        {
            throw new ArgumentException(
                $"Attempt to merge class with different parents: {class1.parent} and {class2.parent}");
        }

        implements = MergeSimple(class1.implements, class2.implements);
        fields = MergeSimple(class1.fields, class2.fields);
        consts = MergeSimple(class1.consts, class2.consts);
        ctors = MergeCtors(class1.ctors, class2.ctors);
        methods =
        [
            ..MergeMethods(class1.methods, class2.methods).Select(x =>
            {
                x.owner = this;
                return x;
            })
        ];
    }


    public string fullName => string.IsNullOrEmpty(pkg) ? name : $"{pkg}.{name}";

    public bool isInterface => classType == ClassType.Interface;

    public bool isLambdaType =>
        classType == ClassType.Interface && fields.Length == 0 && consts.Length == 0 && methods.Length == 1;

    public bool isClassEnum
    {
        get
        {
            if (consts.Length != 0 || ctors.Length != 0 || fields.Length == 0)
                return false;
            var type = fields[0].fieldType;
            return fields.All(x => x.fieldType == type && x.type == (MemberType.Final | MemberType.Static));
        }
    }

    public bool Equals(ClassModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return classType == other.classType && pkg == other.pkg && name == other.name && parent == other.parent &&
               implements.SequenceEqual(other.implements) && ctors.SequenceEqual(other.ctors) &&
               methods.SequenceEqual(other.methods) &&
               fields.SequenceEqual(other.fields) && consts.SequenceEqual(other.consts);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ClassModel other && Equals(other);
    }

    public override int GetHashCode() => fullName.GetHashCode();

    public static bool operator ==(ClassModel? left, ClassModel? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ClassModel? left, ClassModel? right)
    {
        return !Equals(left, right);
    }

    public override string ToString() => fullName;

    public static ImmutableArray<T> MergeSimple<T>(IEnumerable<T> left, IEnumerable<T> right)
        where T : IEquatable<T>
    {
        HashSet<T> set = new();
        foreach (var item in left) set.Add(item);
        foreach (var item in right) set.Add(item);
        return [..set];
    }

    public static ImmutableArray<CtorModel> MergeCtors(IList<CtorModel> left, IList<CtorModel> right)
    {
        List<CtorModel> list = new(left);
        foreach (var ri in right)
        {
            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                var li = list[i];
                if (ri.access == li.access && ri.arguments.SequenceEqual(li.arguments))
                {
                    if (li.throws.SequenceEqual(ri.throws))
                    {
                        // found equal
                        found = true;
                        break;
                    }

                    // merging
                    list[i] = new CtorModel(li.access, MergeSimple(li.throws, ri.throws), li.arguments);
                    found = true;
                    break;
                }
            }

            if (!found)
                list.Add(ri);
        }

        return [..list];
    }

    public static ImmutableArray<MethodModel> MergeMethods(IList<MethodModel> left, IList<MethodModel> right)
    {
        List<MethodModel> list = new(left);
        foreach (var ri in right)
        {
            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                var li = list[i];
                if (ri.access == li.access && ri.HasSameSignature(li) && ri.type == li.type)
                {
                    if (li.throws.SequenceEqual(ri.throws))
                    {
                        // found equal
                        found = true;
                        break;
                    }

                    // merging
                    list[i] = new MethodModel(li.access, MergeSimple(li.throws, ri.throws), li.arguments, li.returnType,
                        li.name, li.type);
                    found = true;
                    break;
                }
            }

            if (!found)
                list.Add(ri);
        }

        return [..list];
    }
}