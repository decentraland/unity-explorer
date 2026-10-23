using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     One csproj of the solution Unity generates: sources, reference paths and defines. Project references
    ///     resolve to the assemblies Unity already compiled under Library/ScriptAssemblies.
    /// </summary>
    internal sealed class UnityProject
    {
        private const string EDITOR_PLATFORM = "Editor";
        private const string SCRIPT_ASSEMBLIES = "Library/ScriptAssemblies";

        // Unity-backed implementations of narrow project APIs (plan § 9, step 1 facades). The shim replaces these
        // assemblies wholesale, so their Unity members are not part of the allowlist.
        private static readonly HashSet<string> FACADE_ASSEMBLIES = new (StringComparer.Ordinal) { "DCL.UnityFacades" };

        // The render bridge and its editor tooling are deleted at the host flip (plan § 7, step 10); they never run on the shim.
        private static readonly HashSet<string> BRIDGE_ASSEMBLIES = new (StringComparer.Ordinal) { "BevyMigration", "BevyMigration.Editor" };

        private static readonly Regex SOLUTION_PROJECT = new (@"^Project\(""\{[^}]+\}""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+\.csproj)""", RegexOptions.Multiline | RegexOptions.Compiled);

        // Compiled from the Editor, the generated csproj defines the editor symbols; the shim never sees that code.
        private static readonly HashSet<string> EDITOR_DEFINES = new (StringComparer.Ordinal)
        {
            "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_OSX", "UNITY_EDITOR_WIN", "UNITY_EDITOR_LINUX",
        };

        public string Name { get; }
        public string ProjectPath { get; }
        public IReadOnlyList<string> Sources { get; }
        public IReadOnlyList<string> ReferencePaths { get; }
        public IReadOnlyList<string> Defines { get; }
        public string LanguageVersion { get; }
        public bool AllowUnsafe { get; }

        private UnityProject(string name, string projectPath, IReadOnlyList<string> sources, IReadOnlyList<string> referencePaths, IReadOnlyList<string> defines, string languageVersion, bool allowUnsafe)
        {
            Name = name;
            ProjectPath = projectPath;
            Sources = sources;
            ReferencePaths = referencePaths;
            Defines = defines;
            LanguageVersion = languageVersion;
            AllowUnsafe = allowUnsafe;
        }

        /// <summary>
        ///     Loads the runtime assemblies of the solution: those whose asmdef lives under Explorer/Assets and is not
        ///     Editor-only. Package and editor assemblies are reported in <paramref name="skipped" /> with the reason.
        /// </summary>
        public static IReadOnlyList<UnityProject> LoadSolution(string solutionPath, string repoRoot, out IReadOnlyDictionary<string, string> skipped)
        {
            string solutionDir = Path.GetDirectoryName(solutionPath) ?? throw new InvalidOperationException($"{solutionPath} has no directory");
            Dictionary<string, AssemblyDefinition> asmdefs = ScanAssemblyDefinitions(Path.Combine(solutionDir, "Assets"));
            var projects = new List<UnityProject>();
            var skippedProjects = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in SOLUTION_PROJECT.Matches(File.ReadAllText(solutionPath)))
            {
                string name = match.Groups["name"].Value;
                string projectPath = Path.GetFullPath(Path.Combine(solutionDir, match.Groups["path"].Value.Replace('\\', '/')));

                if (FACADE_ASSEMBLIES.Contains(name))
                {
                    skippedProjects[name] = "facade assembly: replaced wholesale under the shim (plan § 9)";
                    continue;
                }

                if (BRIDGE_ASSEMBLIES.Contains(name))
                {
                    skippedProjects[name] = "render bridge: deleted at the host flip (plan § 7, step 10)";
                    continue;
                }

                if (!asmdefs.TryGetValue(name, out AssemblyDefinition? asmdef))
                {
                    skippedProjects[name] = "no asmdef under Explorer/Assets (package or predefined assembly)";
                    continue;
                }

                if (asmdef.IsEditorOnly)
                {
                    skippedProjects[name] = $"Editor-only asmdef ({Path.GetRelativePath(repoRoot, asmdef.Path)})";
                    continue;
                }

                projects.Add(Load(name, projectPath, solutionDir));
            }

            projects.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            skipped = skippedProjects;
            return projects;
        }

        private static UnityProject Load(string name, string projectPath, string solutionDir)
        {
            XDocument document = XDocument.Load(projectPath);
            string projectDir = Path.GetDirectoryName(projectPath) ?? solutionDir;
            var sources = new List<string>();
            var references = new List<string>();
            var defines = new List<string>();
            var languageVersion = "latest";
            var allowUnsafe = false;

            foreach (XElement element in document.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case "Compile":
                        string? include = element.Attribute("Include")?.Value;
                        if (include != null) sources.Add(Path.GetFullPath(Path.Combine(projectDir, include.Replace('\\', '/'))));
                        break;
                    case "HintPath":
                        references.Add(Path.GetFullPath(element.Value.Replace('\\', '/')));
                        break;
                    case "ProjectReference":
                        string? referenced = element.Attribute("Include")?.Value;
                        if (referenced != null)
                            references.Add(Path.GetFullPath(Path.Combine(solutionDir, SCRIPT_ASSEMBLIES, Path.GetFileNameWithoutExtension(referenced) + ".dll")));
                        break;
                    case "DefineConstants":
                        foreach (string define in element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            if (!EDITOR_DEFINES.Contains(define))
                                defines.Add(define);
                        break;
                    case "LangVersion":
                        languageVersion = element.Value.Trim();
                        break;
                    case "AllowUnsafeBlocks":
                        allowUnsafe = string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            sources.Sort(StringComparer.Ordinal);
            return new UnityProject(name, projectPath, sources, references, defines, languageVersion, allowUnsafe);
        }

        private static Dictionary<string, AssemblyDefinition> ScanAssemblyDefinitions(string assetsDir)
        {
            var result = new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);

            foreach (string path in Directory.EnumerateFiles(assetsDir, "*.asmdef", SearchOption.AllDirectories))
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
                string name = json.RootElement.GetProperty("name").GetString() ?? throw new InvalidDataException($"{path}: asmdef without a name");
                var editorOnly = false;

                if (json.RootElement.TryGetProperty("includePlatforms", out JsonElement platforms) && platforms.GetArrayLength() == 1)
                    editorOnly = platforms[0].GetString() == EDITOR_PLATFORM;

                result[name] = new AssemblyDefinition(path, editorOnly);
            }

            return result;
        }

        private sealed record AssemblyDefinition(string Path, bool IsEditorOnly);
    }
}
