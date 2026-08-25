using System.Collections.Concurrent;
using System.Reflection;

namespace DynTypeSerializer.Serialization;

/// <summary>
/// Shared type/member metadata helpers used by both the JSON and binary
/// serializers. Kept in one place so the two formats stay consistent.
/// </summary>
internal static class SerializerCore
{
    /// <summary>
    /// Types whose values are leaf nodes — do NOT recurse into their properties.
    /// </summary>
    internal static bool IsPrimitiveLike(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive
            || t == typeof(string)
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan)
            || t == typeof(Guid)
            || t == typeof(Uri)
            || t == typeof(Version)
            || t.IsEnum;
    }

    internal static PropertyInfo[] GetProperties(Type t)
        => PropCache.GetOrAdd(t, static type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray());

    /// <summary>
    /// Should a runtime type tag be emitted for this value?
    /// </summary>
    internal static bool NeedsTypeTag(Type actualType, Type declaredType)
    {
        if (declaredType == typeof(object)) return true;
        if (declaredType.IsInterface) return true;
        if (declaredType.IsAbstract) return true;
        return actualType != declaredType;
    }

    internal static string GetTypeCode(Type t, Serializer.Options? options = null)
    {
        if (options?.IncludeFullAssemblyInfo == true)
            return t.AssemblyQualifiedName ?? t.FullName ?? t.Name;

        if (TypeToCode.TryGetValue(t, out var code))
            return code;

        return t.FullName ?? t.Name;
    }

    internal static Type ResolveType(string code)
    {
        // 1. Short code table
        if (CodeToType.TryGetValue(code, out var t)) return t;

        // 2. Cache hit
        if (NameToType.TryGetValue(code, out t)) return t!;

        // 3. Type.GetType (handles assembly-qualified names)
        t = Type.GetType(code);
        if (t is not null) { NameToType[code] = t; return t; }

        // 4. Scan loaded assemblies by FullName or Name
        t = AppDomain.CurrentDomain
                     .GetAssemblies()
                     .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                     .FirstOrDefault(x => x.FullName == code || x.Name == code);

        if (t is not null) { NameToType[code] = t; return t; }

        Serializer.LogWarning(TypeCodeWarning.UnresolvedType, (code, string.Empty));
        throw new InvalidOperationException(
            $"DynTypeSerializer: cannot resolve type '{code}'. " +
            $"If this is a user type, ensure the assembly is loaded.");
    }

    // ── Short type codes ────────────────────────────────────────────────────
    private static readonly Dictionary<Type, string> TypeToCode = new()
    {
        [typeof(bool)]           = "b",
        [typeof(bool?)]          = "b?",
        [typeof(byte)]           = "by",
        [typeof(byte?)]          = "by?",
        [typeof(sbyte)]          = "sb",
        [typeof(sbyte?)]         = "sb?",
        [typeof(char)]           = "c",
        [typeof(char?)]          = "c?",
        [typeof(short)]          = "sh",
        [typeof(short?)]         = "sh?",
        [typeof(ushort)]         = "ush",
        [typeof(ushort?)]        = "ush?",
        [typeof(int)]            = "i",
        [typeof(int?)]           = "i?",
        [typeof(uint)]           = "ui",
        [typeof(uint?)]          = "ui?",
        [typeof(long)]           = "l",
        [typeof(long?)]          = "l?",
        [typeof(ulong)]          = "ul",
        [typeof(ulong?)]         = "ul?",
        [typeof(float)]          = "f",
        [typeof(float?)]         = "f?",
        [typeof(double)]         = "d",
        [typeof(double?)]        = "d?",
        [typeof(decimal)]        = "dec",
        [typeof(decimal?)]       = "dec?",
        [typeof(string)]         = "s",
        [typeof(DateTime)]       = "dt",
        [typeof(DateTime?)]      = "dt?",
        [typeof(DateTimeOffset)] = "dto",
        [typeof(DateTimeOffset?)]= "dto?",
        [typeof(TimeSpan)]       = "ts",
        [typeof(TimeSpan?)]      = "ts?",
        [typeof(Guid)]           = "g",
        [typeof(Guid?)]          = "g?",
        [typeof(Uri)]            = "uri",
        [typeof(Version)]        = "ver",
        [typeof(object)]         = "o",
        [typeof(object[])]       = "oa",
    };

    private static readonly Dictionary<string, Type> CodeToType =
        TypeToCode.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly ConcurrentDictionary<string, Type> NameToType = new();

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropCache = new();
}
