using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     Every Unity member the runtime assemblies use, with files and counts, written to ledger/api-inventory.json.
    ///     The JSON carries no timestamp so that regenerating it without code changes is a no-op diff.
    /// </summary>
    internal sealed class Inventory
    {
        private const int SCHEMA_VERSION = 1;

        public IReadOnlyList<string> Assemblies { get; }
        public IReadOnlyDictionary<string, string> SkippedAssemblies { get; }
        public IReadOnlyDictionary<string, int> UnresolvedPerAssembly { get; }
        public SortedDictionary<string, MemberUse> Members { get; }

        internal Inventory(IReadOnlyList<string> assemblies, IReadOnlyDictionary<string, string> skippedAssemblies, IReadOnlyDictionary<string, int> unresolvedPerAssembly, SortedDictionary<string, MemberUse> members)
        {
            Assemblies = assemblies;
            SkippedAssemblies = skippedAssemblies;
            UnresolvedPerAssembly = unresolvedPerAssembly;
            Members = members;
        }

        /// <param name="unresolvedReport">When set, every unresolved name is counted and written there, most frequent first.</param>
        public static Inventory Collect(IReadOnlyList<UnityProject> projects, string repoRoot, IReadOnlyDictionary<string, string> skipped, string? unresolvedReport = null)
        {
            var referenceCache = new ConcurrentDictionary<string, MetadataReference>(StringComparer.Ordinal);
            var perProject = new ConcurrentDictionary<string, Dictionary<string, MemberUse>>(StringComparer.Ordinal);
            var unresolved = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var unresolvedNames = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

            Parallel.ForEach(projects, project =>
            {
                CSharpCompilation compilation = Compile(project, referenceCache);
                var members = new Dictionary<string, MemberUse>(StringComparer.Ordinal);
                var unresolvedCount = 0;
                Dictionary<string, int>? names = unresolvedReport != null ? new Dictionary<string, int>(StringComparer.Ordinal) : null;

                foreach (SyntaxTree tree in compilation.SyntaxTrees)
                {
                    string file = Path.GetRelativePath(repoRoot, tree.FilePath).Replace('\\', '/');
                    var collector = new MemberCollector(compilation.GetSemanticModel(tree, ignoreAccessibility: true), members, file, names);
                    collector.Visit(tree.GetRoot());
                    unresolvedCount += collector.Unresolved;
                }

                if (names != null)
                    foreach (KeyValuePair<string, int> name in names)
                        unresolvedNames.AddOrUpdate(name.Key, name.Value, (_, seen) => seen + name.Value);

                perProject[project.Name] = members;
                unresolved[project.Name] = unresolvedCount;
                Console.WriteLine($"  {project.Name}: {project.Sources.Count} files, {members.Count} members, {unresolvedCount} unresolved names");
            });

            var merged = new SortedDictionary<string, MemberUse>(StringComparer.Ordinal);
            var assemblies = new List<string>();

            foreach (UnityProject project in projects)
            {
                assemblies.Add(project.Name);

                foreach (KeyValuePair<string, MemberUse> entry in perProject[project.Name])
                {
                    if (merged.TryGetValue(entry.Key, out MemberUse? existing))
                        existing.Merge(entry.Value);
                    else
                        merged[entry.Key] = entry.Value;
                }
            }

            if (unresolvedReport != null)
                WriteUnresolved(unresolvedReport, unresolvedNames);

            return new Inventory(assemblies, skipped, new SortedDictionary<string, int>(unresolved, StringComparer.Ordinal), merged);
        }

        private static void WriteUnresolved(string path, ConcurrentDictionary<string, int> names)
        {
            var sorted = new List<KeyValuePair<string, int>>(names);
            sorted.Sort(static (a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var lines = new List<string>(sorted.Count);

            foreach (KeyValuePair<string, int> entry in sorted)
                lines.Add($"{entry.Value}\t{entry.Key}");

            File.WriteAllLines(path, lines);
        }

        public static Inventory FromMembers(IEnumerable<MemberUse> members)
        {
            var sorted = new SortedDictionary<string, MemberUse>(StringComparer.Ordinal);

            foreach (MemberUse member in members)
                sorted[member.Id] = member;

            return new Inventory(Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, int>(), sorted);
        }

        public void WriteJson(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            using FileStream stream = File.Create(path);
            using var writer = new Utf8JsonWriter(stream, options);

            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SCHEMA_VERSION);
            writer.WriteStartArray("assemblies");
            foreach (string assembly in Assemblies) writer.WriteStringValue(assembly);
            writer.WriteEndArray();
            writer.WriteStartObject("skippedAssemblies");
            foreach (KeyValuePair<string, string> entry in SkippedAssemblies) writer.WriteString(entry.Key, entry.Value);
            writer.WriteEndObject();
            writer.WriteStartObject("unresolvedNames");
            foreach (KeyValuePair<string, int> entry in UnresolvedPerAssembly) writer.WriteNumber(entry.Key, entry.Value);
            writer.WriteEndObject();
            writer.WriteNumber("memberCount", Members.Count);
            writer.WriteStartArray("members");

            foreach (MemberUse member in Members.Values)
            {
                writer.WriteStartObject();
                writer.WriteString("id", member.Id);
                writer.WriteString("kind", member.Kind);
                writer.WriteString("group", SubsystemGroups.GroupOf(member.Root, member.ContainingType));
                writer.WriteNumber("count", member.Count);
                writer.WriteStartObject("files");
                foreach (KeyValuePair<string, int> file in member.Files) writer.WriteNumber(file.Key, file.Value);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            stream.WriteByte((byte)'\n');
        }

        private static CSharpCompilation Compile(UnityProject project, ConcurrentDictionary<string, MetadataReference> referenceCache)
        {
            LanguageVersion languageVersion = LanguageVersionFacts.TryParse(project.LanguageVersion, out LanguageVersion parsed) ? parsed : LanguageVersion.Latest;
            var parseOptions = new CSharpParseOptions(languageVersion, preprocessorSymbols: project.Defines);
            var trees = new List<SyntaxTree>(project.Sources.Count);

            foreach (string source in project.Sources)
                if (File.Exists(source))
                    trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(source), parseOptions, source));

            var references = new List<MetadataReference>(project.ReferencePaths.Count);

            foreach (string path in project.ReferencePaths)
                if (File.Exists(path))
                    references.Add(referenceCache.GetOrAdd(path, static p => MetadataReference.CreateFromFile(p)));

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: project.AllowUnsafe);
            return CSharpCompilation.Create(project.Name, trees, references, options);
        }
    }
}
