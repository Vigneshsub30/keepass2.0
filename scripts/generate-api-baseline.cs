// Standalone script: generate KeePassLib.PublicApi.txt
// Run with: dotnet-script generate-api-baseline.cs <KeePassLib.dll>
// Or just run directly: dotnet run -- <KeePassLib.dll>
//
// This file is run from the repository root as:
//   dotnet script scripts/generate-api-baseline.cs KeePassLib/bin/Debug/net10.0-windows/KeePassLib.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

var dllPath = args.Length > 0 ? args[0]
    : "KeePassLib/bin/Debug/net10.0-windows/KeePassLib.dll";

if (!File.Exists(dllPath))
{
    Console.Error.WriteLine($"Not found: {dllPath}");
    return 1;
}

var asm = Assembly.LoadFrom(Path.GetFullPath(dllPath));
var sigs = new SortedSet<string>(StringComparer.Ordinal);

const BindingFlags flags =
    BindingFlags.Public | BindingFlags.NonPublic |
    BindingFlags.Instance | BindingFlags.Static |
    BindingFlags.DeclaredOnly;

foreach (var t in asm.GetExportedTypes())
{
    sigs.Add(FmtType(t));
    foreach (var m in t.GetMembers(flags))
    {
        if (!IsPubProt(m)) continue;
        var s = FmtMember(t, m);
        if (s != null) sigs.Add(s);
    }
}

var outFile = Path.Combine("KeePass.Tests", "Baselines", "KeePassLib.PublicApi.txt");
Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
File.WriteAllLines(outFile, sigs, Encoding.UTF8);
Console.WriteLine($"Wrote {sigs.Count} entries → {outFile}");
return 0;

// ── helpers ──────────────────────────────────────────────────────────────

static bool IsPubProt(MemberInfo m) => m switch
{
    MethodBase mb => mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly,
    PropertyInfo pi => (pi.GetGetMethod(true) is { } g && (g.IsPublic || g.IsFamily || g.IsFamilyOrAssembly))
                    || (pi.GetSetMethod(true) is { } s && (s.IsPublic || s.IsFamily || s.IsFamilyOrAssembly)),
    EventInfo ei => ei.GetAddMethod(true) is { } a && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
    FieldInfo fi => fi.IsPublic || fi.IsFamily || fi.IsFamilyOrAssembly,
    _ => false
};

static string FmtType(Type t)
{
    var kind = t.IsEnum ? "enum"
        : t.IsInterface ? "interface"
        : t.IsValueType ? "struct"
        : t.IsAbstract && t.IsSealed ? "static class"
        : t.IsAbstract ? "abstract class"
        : t.IsSealed ? "sealed class"
        : "class";
    return $"T {t.FullName} [{kind}]";
}

static string? FmtMember(Type t, MemberInfo m)
{
    switch (m)
    {
        case ConstructorInfo ci:
            return $"M {t.FullName}..ctor({FmtParams(ci.GetParameters())})";
        case MethodInfo mi:
            if (mi.IsSpecialName) return null;
            var gen = mi.IsGenericMethod ? "<" + string.Join(",", Array.ConvertAll(mi.GetGenericArguments(), FmtTn)) + ">" : "";
            return $"M {t.FullName}.{mi.Name}{gen}({FmtParams(mi.GetParameters())}) : {FmtTn(mi.ReturnType)}";
        case PropertyInfo pi:
            var hasGet = pi.CanRead && IsPubProt(pi.GetGetMethod(true)!);
            var hasSet = pi.CanWrite && IsPubProt(pi.GetSetMethod(true)!);
            return $"P {t.FullName}.{pi.Name} : {FmtTn(pi.PropertyType)} [{(hasGet?"get;":"")}{(hasSet?"set;":"")}]";
        case EventInfo ei:
            return $"E {t.FullName}.{ei.Name} : {FmtTn(ei.EventHandlerType)}";
        case FieldInfo fi:
            if (fi.Name.Contains('<') || fi.Name.Contains('>')) return null;
            var mods = fi.IsLiteral ? "const"
                : fi.IsStatic && fi.IsInitOnly ? "static readonly"
                : fi.IsStatic ? "static"
                : fi.IsInitOnly ? "readonly"
                : "";
            return $"F {t.FullName}.{fi.Name} : {FmtTn(fi.FieldType)} [{mods}]";
        default:
            return null;
    }
}

static string FmtParams(ParameterInfo[] ps) =>
    ps.Length == 0 ? "" : string.Join(", ", Array.ConvertAll(ps, p => FmtTn(p.ParameterType)));

static string FmtTn(Type t)
{
    if (t == null || t == typeof(void)) return "void";
    if (t.IsGenericParameter) return t.Name;
    if (t.IsArray) return FmtTn(t.GetElementType()!) + "[]";
    if (t.IsByRef) return FmtTn(t.GetElementType()!) + "&";
    if (t.IsPointer) return FmtTn(t.GetElementType()!) + "*";
    if (t.IsGenericType)
    {
        var bn = t.GetGenericTypeDefinition().FullName ?? t.Name;
        var ti = bn.IndexOf('`');
        if (ti >= 0) bn = bn[..ti];
        return $"{bn}<{string.Join(",", Array.ConvertAll(t.GetGenericArguments(), FmtTn))}>";
    }
    return t.FullName ?? t.Name;
}
