using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DCL.BevyMigration.Inventory
{
    /// <summary>
    ///     Writes ledger/shader-families.md: every shader seen with its family, material count and renderer count,
    ///     then the per-family totals the step 1 gate asks for (BevyMigration/docs/README.md § 7).
    /// </summary>
    public static class ShaderFamilyReport
    {
        public const string DEFAULT_FILE_NAME = "shader-families.md";

        /// <summary>
        ///     The ledger file relative to the Unity project: only meaningful when running from the repository.
        /// </summary>
        public static string LedgerPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "BevyMigration", "ledger", DEFAULT_FILE_NAME));

        public readonly struct Entry
        {
            public readonly string ShaderName;
            public readonly int Materials;
            public readonly int Renderers;

            public Entry(string shaderName, int materials, int renderers)
            {
                ShaderName = shaderName;
                Materials = materials;
                Renderers = renderers;
            }
        }

        public static void Write(string path, string source, IReadOnlyList<Entry> entries)
        {
            var sorted = new List<Entry>(entries);
            sorted.Sort(static (a, b) => b.Renderers != a.Renderers ? b.Renderers.CompareTo(a.Renderers) : string.CompareOrdinal(a.ShaderName, b.ShaderName));

            var families = new Dictionary<ShaderFamily, FamilyTotals>();

            foreach (Entry entry in sorted)
            {
                ShaderFamily family = ShaderFamilies.Classify(entry.ShaderName);
                families.TryGetValue(family, out FamilyTotals totals);
                families[family] = new FamilyTotals(totals.Shaders + 1, totals.Materials + entry.Materials, totals.Renderers + entry.Renderers);
            }

            var builder = new StringBuilder();
            builder.Append("# Shader families\n\n");
            builder.Append("Source: ").Append(source).Append("  \n");
            builder.Append("Unity: ").Append(Application.unityVersion).Append("  \n");
            builder.Append("Captured: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).Append("  \n");
            builder.Append("Regenerate: menu `Decentraland/Bevy Migration/Shader Inventory`, or run a player build with `--shader-inventory[=<path>]` (BevyMigration/docs/README.md § 7, step 1).\n\n");

            builder.Append("## Families\n\n| Family | Shaders | Materials | Renderers | Ported in step |\n|---|---:|---:|---:|---|\n");

            foreach (ShaderFamily family in (ShaderFamily[])Enum.GetValues(typeof(ShaderFamily)))
            {
                if (!families.TryGetValue(family, out FamilyTotals totals)) continue;

                int? step = ShaderFamilies.PortingStep(family);
                builder.Append("| ").Append(family).Append(" | ").Append(totals.Shaders).Append(" | ").Append(totals.Materials).Append(" | ").Append(totals.Renderers)
                       .Append(" | ").Append(step.HasValue ? step.Value.ToString(CultureInfo.InvariantCulture) : "never").Append(" |\n");
            }

            builder.Append("\n## Shaders\n\n| Shader | Family | Materials | Renderers |\n|---|---|---:|---:|\n");

            foreach (Entry entry in sorted)
                builder.Append("| ").Append(entry.ShaderName.Replace("|", "\\|")).Append(" | ").Append(ShaderFamilies.Classify(entry.ShaderName))
                       .Append(" | ").Append(entry.Materials).Append(" | ").Append(entry.Renderers).Append(" |\n");

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, builder.ToString());
        }

        private readonly struct FamilyTotals
        {
            public readonly int Shaders;
            public readonly int Materials;
            public readonly int Renderers;

            public FamilyTotals(int shaders, int materials, int renderers)
            {
                Shaders = shaders;
                Materials = materials;
                Renderers = renderers;
            }
        }
    }
}
