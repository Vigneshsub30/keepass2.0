using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace KeePass.Tests.ApiSurface
{
    /// <summary>
    /// Snapshot test that guards the KeePassLib public API surface against
    /// accidental removals or signature changes.
    ///
    /// Additions to the public surface are allowed.
    /// Removals or signature changes fail the test with a diff listing.
    ///
    /// To update the baseline after an intentional API change:
    ///   dotnet test --filter "FullyQualifiedName~RegenerateBaseline" /p:RegenerateApiBaseline=true
    /// Or simply delete KeePass.Tests/Baselines/KeePassLib.PublicApi.txt and
    /// run the tests once — the test auto-creates a new baseline on first run.
    /// </summary>
    public class PublicApiSnapshotTests
    {
        // AppContext.BaseDirectory = KeePass.Tests/bin/{config}/net10.0-windows/
        // Three levels up reaches KeePass.Tests/ — then Baselines/ is a subdirectory.
        private static readonly string s_baselineDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Baselines"));

        private static readonly string s_baselineFile = Path.GetFullPath(
            Path.Combine(s_baselineDir, "KeePassLib.PublicApi.txt"));

        // ── Meta-test ────────────────────────────────────────────────────────

        [Fact]
        public void Baseline_IsNotEmpty_AndHasAtLeast166Members()
        {
            string[] lines = ReadBaselineLines();
            Assert.True(lines.Length >= 166,
                $"Baseline has only {lines.Length} entries; expected at least 166. " +
                "Regenerate the baseline file if this is a new project state.");
        }

        // ── Snapshot comparison test ─────────────────────────────────────────

        [Fact]
        public void KeePassLib_PublicApiSurface_MatchesBaseline()
        {
            string[] baseline = ReadBaselineLines();
            HashSet<string> baselineSet = new HashSet<string>(
                baseline, StringComparer.Ordinal);

            List<string> current = ExtractPublicSurface();
            HashSet<string> currentSet = new HashSet<string>(
                current, StringComparer.Ordinal);

            // Collect any symbol that was in the baseline but is no longer present
            List<string> removed = new List<string>();
            foreach (string sym in baselineSet)
            {
                if (!currentSet.Contains(sym))
                    removed.Add(sym);
            }

            if (removed.Count > 0)
            {
                removed.Sort(StringComparer.Ordinal);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(
                    $"{removed.Count} public symbol(s) were removed or their signatures changed:");
                foreach (string sym in removed)
                    sb.AppendLine($"  - {sym}");
                sb.AppendLine();
                sb.AppendLine("If this is intentional, update the baseline by deleting");
                sb.AppendLine(s_baselineFile);
                sb.AppendLine("and re-running the tests to regenerate it.");
                Assert.Fail(sb.ToString());
            }
        }

        // ── Baseline generation ──────────────────────────────────────────────

        /// <summary>
        /// Generates (or regenerates) the baseline snapshot file.
        /// Run this test manually after intentional API additions or when
        /// seeding the snapshot for the first time.
        /// </summary>
        [Fact]
        public void RegenerateBaseline()
        {
            // Skip in normal CI; only run explicitly or when baseline is missing.
            bool baselineMissing = !File.Exists(s_baselineFile);
            bool explicitRegen = string.Equals(
                Environment.GetEnvironmentVariable("REGENERATE_API_BASELINE"),
                "true", StringComparison.OrdinalIgnoreCase);

            if (!baselineMissing && !explicitRegen)
                return; // Intentionally do nothing in normal runs

            List<string> lines = ExtractPublicSurface();
            Directory.CreateDirectory(s_baselineDir);
            // UTF-8 without BOM for maximum portability and clean diffs
            File.WriteAllLines(s_baselineFile, lines, new UTF8Encoding(false));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string[] ReadBaselineLines()
        {
            if (!File.Exists(s_baselineFile))
                throw new FileNotFoundException(
                    $"Baseline file not found: {s_baselineFile}. " +
                    "Run the RegenerateBaseline test once to create it.");

            return File.ReadAllLines(s_baselineFile);
        }

        /// <summary>
        /// Extracts a sorted list of signature strings for every public member
        /// declared on every public type exported by KeePassLib.
        /// Includes: methods (including constructors and operators), properties,
        /// events, and fields. Protected members are included because they form
        /// part of the contract for derived types.
        /// </summary>
        private static List<string> ExtractPublicSurface()
        {
            Assembly asm = typeof(KeePassLib.PwDatabase).Assembly;
            Type[] exportedTypes = asm.GetExportedTypes();

            SortedSet<string> signatures = new SortedSet<string>(
                StringComparer.Ordinal);

            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |   // includes Protected
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            foreach (Type t in exportedTypes)
            {
                // Record the type itself
                signatures.Add(FormatType(t));

                foreach (MemberInfo m in t.GetMembers(flags))
                {
                    // Restrict to public and family (protected)
                    if (!IsPublicOrProtected(m)) continue;

                    string sig = FormatMember(t, m);
                    if (sig != null)
                        signatures.Add(sig);
                }
            }

            return new List<string>(signatures);
        }

        private static bool IsPublicOrProtected(MemberInfo m)
        {
            switch (m)
            {
                case MethodBase mb:
                    return mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly;
                case PropertyInfo pi:
                    MethodInfo getter = pi.GetGetMethod(nonPublic: true);
                    MethodInfo setter = pi.GetSetMethod(nonPublic: true);
                    return (getter != null && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly)) ||
                           (setter != null && (setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly));
                case EventInfo ei:
                    MethodInfo add = ei.GetAddMethod(nonPublic: true);
                    return add != null && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly);
                case FieldInfo fi:
                    return fi.IsPublic || fi.IsFamily || fi.IsFamilyOrAssembly;
                default:
                    return false;
            }
        }

        private static string FormatType(Type t)
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

        private static string FormatMember(Type t, MemberInfo m)
        {
            switch (m)
            {
                case ConstructorInfo ci:
                    return $"M {t.FullName}..ctor({FormatParams(ci.GetParameters())})";

                case MethodInfo mi:
                    // Skip compiler-generated (property/event accessors, etc.)
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
                    string accessors = (hasGet ? "get;" : "") + (hasSet ? "set;" : "");
                    return $"P {t.FullName}.{pi.Name} : {propType} [{accessors}]";

                case EventInfo ei:
                    string evType = FormatTypeName(ei.EventHandlerType);
                    return $"E {t.FullName}.{ei.Name} : {evType}";

                case FieldInfo fi:
                    // Skip compiler-generated backing fields for properties/events
                    if (fi.Name.Contains("<") || fi.Name.Contains(">")) return null;
                    string fieldType = FormatTypeName(fi.FieldType);
                    string fieldMods = fi.IsLiteral ? "const"
                        : fi.IsStatic && fi.IsInitOnly ? "static readonly"
                        : fi.IsStatic ? "static"
                        : fi.IsInitOnly ? "readonly"
                        : "";
                    return $"F {t.FullName}.{fi.Name} : {fieldType} [{fieldMods}]";

                default:
                    return null;
            }
        }

        private static string FormatParams(ParameterInfo[] ps)
        {
            if (ps == null || ps.Length == 0) return "";
            string[] parts = new string[ps.Length];
            for (int i = 0; i < ps.Length; ++i)
                parts[i] = FormatTypeName(ps[i].ParameterType);
            return string.Join(", ", parts);
        }

        private static string FormatTypeName(Type t)
        {
            if (t == null) return "void";
            if (t == typeof(void)) return "void";
            if (t.IsGenericParameter) return t.Name;

            if (t.IsArray)
                return FormatTypeName(t.GetElementType()) + "[]";

            if (t.IsByRef)
                return FormatTypeName(t.GetElementType()) + "&";

            if (t.IsPointer)
                return FormatTypeName(t.GetElementType()) + "*";

            if (t.IsGenericType)
            {
                string baseName = t.GetGenericTypeDefinition().FullName ?? t.Name;
                int tick = baseName.IndexOf('`');
                if (tick >= 0) baseName = baseName.Substring(0, tick);
                string typeArgs = string.Join(",", Array.ConvertAll(
                    t.GetGenericArguments(), FormatTypeName));
                return $"{baseName}<{typeArgs}>";
            }

            return t.FullName ?? t.Name;
        }
    }
}
