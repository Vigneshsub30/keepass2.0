#!/usr/bin/env dotnet-script
// Cross-platform script to generate KeePassLib.PublicApi.txt baseline.
// Run from repository root:
//   dotnet script scripts/GenerateApiBaseline.csx
// Or use the .NET reflection approach via the helper project.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

string dllPath = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "KeePassLib.dll");

if (!File.Exists(dllPath))
{
    Console.Error.WriteLine($"KeePassLib.dll not found at: {dllPath}");
    Console.Error.WriteLine("Build KeePassLib first, then run:");
    Console.Error.WriteLine("  dotnet script scripts/GenerateApiBaseline.csx <path-to-KeePassLib.dll>");
    Environment.Exit(1);
}

Assembly asm = Assembly.LoadFrom(dllPath);
Type[] exportedTypes = asm.GetExportedTypes();

const BindingFlags flags =
    BindingFlags.Public | BindingFlags.NonPublic |
    BindingFlags.Instance | BindingFlags.Static |
    BindingFlags.DeclaredOnly;

SortedSet<string> signatures = new SortedSet<string>(StringComparer.Ordinal);

foreach (Type t in exportedTypes)
{
    signatures.Add(FormatType(t));
    foreach (MemberInfo m in t.GetMembers(flags))
    {
        if (!IsPublicOrProtected(m)) continue;
        string sig = FormatMember(t, m);
        if (sig != null) signatures.Add(sig);
    }
}

string outDir = Path.Combine(
    Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)),
    "KeePass.Tests", "Baselines");

string outFile = Path.Combine(outDir, "KeePassLib.PublicApi.txt");
Directory.CreateDirectory(outDir);
File.WriteAllLines(outFile, signatures, Encoding.UTF8);

Console.WriteLine($"Wrote {signatures.Count} entries to:");
Console.WriteLine(outFile);

// ── Helpers ──────────────────────────────────────────────────────────────

static bool IsPublicOrProtected(MemberInfo m)
{
    switch (m)
    {
        case MethodBase mb:
            return mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly;
        case PropertyInfo pi:
            MethodInfo getter = pi.GetGetMethod(true);
            MethodInfo setter = pi.GetSetMethod(true);
            return (getter != null && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly)) ||
                   (setter != null && (setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly));
        case EventInfo ei:
            MethodInfo add = ei.GetAddMethod(true);
            return add != null && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly);
        case FieldInfo fi:
            return fi.IsPublic || fi.IsFamily || fi.IsFamilyOrAssembly;
        default:
            return false;
    }
}

static string FormatType(Type t)
{
    string kind = t.IsEnum ? "enum"
        : t.IsInterface ? "interface"
        : t.IsValueType ? "struct"
        : t.IsAbstract && t.IsSealed ? "static class"
        : t.IsAbstract ? "abstract class"
        : t.IsSealed ? "sealed class"
        : "class";
    return $"T {t.FullName} [{kind}]";
}

static string FormatMember(Type t, MemberInfo m)
{
    switch (m)
    {
        case ConstructorInfo ci:
            return $"M {t.FullName}..ctor({FormatParams(ci.GetParameters())})";
        case MethodInfo mi:
            if (mi.IsSpecialName) return null;
            string retType = FormatTypeName(mi.ReturnType);
            string generic = mi.IsGenericMethod
                ? "<" + string.Join(",", Array.ConvertAll(mi.GetGenericArguments(), FormatTypeName)) + ">"
                : "";
            return $"M {t.FullName}.{mi.Name}{generic}({FormatParams(mi.GetParameters())}) : {retType}";
        case PropertyInfo pi:
            string propType = FormatTypeName(pi.PropertyType);
            bool hasGet = pi.CanRead && IsPublicOrProtected(pi.GetGetMethod(true));
            bool hasSet = pi.CanWrite && IsPublicOrProtected(pi.GetSetMethod(true));
            return $"P {t.FullName}.{pi.Name} : {propType} [{(hasGet ? "get;" : "")}{(hasSet ? "set;" : "")}]";
        case EventInfo ei:
            return $"E {t.FullName}.{ei.Name} : {FormatTypeName(ei.EventHandlerType)}";
        case FieldInfo fi:
            if (fi.Name.Contains("<") || fi.Name.Contains(">")) return null;
            string mods = fi.IsLiteral ? "const"
                : fi.IsStatic && fi.IsInitOnly ? "static readonly"
                : fi.IsStatic ? "static"
                : fi.IsInitOnly ? "readonly"
                : "";
            return $"F {t.FullName}.{fi.Name} : {FormatTypeName(fi.FieldType)} [{mods}]";
        default:
            return null;
    }
}

static string FormatParams(ParameterInfo[] ps)
{
    if (ps == null || ps.Length == 0) return "";
    return string.Join(", ", Array.ConvertAll(ps, p => FormatTypeName(p.ParameterType)));
}

static string FormatTypeName(Type t)
{
    if (t == null || t == typeof(void)) return "void";
    if (t.IsGenericParameter) return t.Name;
    if (t.IsArray) return FormatTypeName(t.GetElementType()) + "[]";
    if (t.IsByRef) return FormatTypeName(t.GetElementType()) + "&";
    if (t.IsPointer) return FormatTypeName(t.GetElementType()) + "*";
    if (t.IsGenericType)
    {
        string baseName = t.GetGenericTypeDefinition().FullName ?? t.Name;
        int tick = baseName.IndexOf('`');
        if (tick >= 0) baseName = baseName.Substring(0, tick);
        return $"{baseName}<{string.Join(",", Array.ConvertAll(t.GetGenericArguments(), FormatTypeName))}>";
    }
    return t.FullName ?? t.Name;
}
