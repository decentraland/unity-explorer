#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

// ReSharper disable InconsistentNaming (GateConfig/GateEntry fields follow the config JSON wire format)
namespace DCL.Editor
{
    /// <summary>
    ///     Batchmode static gate for shader-optimization rounds: compiles configured shader variants for
    ///     D3D11 through the public ShaderData API and writes per-variant reports (compile messages, DXBC
    ///     byte size, DXBC STAT-chunk instruction counts, resource bindings) plus the raw bytecode for
    ///     external disassembly. Diff the reports between two revisions to prove a change is a compiler no-op.
    ///     Invoke with:
    ///     Unity -batchmode -projectPath ... -executeMethod DCL.Editor.ShaderStaticGate.Run
    ///     -shaderGateConfig path\to\config.json -shaderGateOutput path\to\outDir
    ///     (-nographics must NOT be passed: shader compilation needs the graphics device.)
    /// </summary>
    public static class ShaderStaticGate
    {
        private const string CONFIG_ARG = "-shaderGateConfig";
        private const string OUTPUT_ARG = "-shaderGateOutput";
        private const string SUMMARY_FILE_NAME = "gate-summary.txt";

        private static readonly ShaderType[] STAGES = { ShaderType.Vertex, ShaderType.Fragment };

        // D3D11 DXBC STAT chunk dword labels (well-known d3d11 shader statistics layout); unknown
        // indices are still dumped raw — the gate diffs values, labels are a convenience.
        private static readonly Dictionary<int, string> STAT_LABELS = new ()
        {
            [0] = "InstructionCount",
            [1] = "TempRegisterCount",
            [2] = "DefineCount",
            [3] = "DeclarationCount",
            [4] = "FloatInstructionCount",
            [5] = "IntInstructionCount",
            [6] = "UintInstructionCount",
            [7] = "StaticFlowControlCount",
            [8] = "DynamicFlowControlCount",
            [10] = "TempArrayCount",
            [11] = "ArrayInstructionCount",
            [14] = "TextureNormalInstructions",
            [15] = "TextureLoadInstructions",
            [16] = "TextureComparisonInstructions",
            [17] = "TextureBiasInstructions",
            [18] = "TextureGradientInstructions",
            [19] = "MovInstructionCount",
            [20] = "MovcInstructionCount",
            [21] = "ConversionInstructionCount",
        };

        public static void Run()
        {
            var exitCode = 0;

            try
            {
                exitCode = RunInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShaderStaticGate] {ex}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static int RunInternal()
        {
            string? configPath = GetArgValue(CONFIG_ARG);

            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"{CONFIG_ARG} <json path> is required and must exist (got: '{configPath}').");

            GateConfig? config = JsonUtility.FromJson<GateConfig>(File.ReadAllText(configPath));

            if (config?.entries == null || config.entries.Length == 0)
                throw new ArgumentException($"No entries found in {configPath}.");

            string outputDir = GetArgValue(OUTPUT_ARG) ?? config.outputDir;

            if (string.IsNullOrEmpty(outputDir))
                throw new ArgumentException($"{OUTPUT_ARG} <dir> (or \"outputDir\" in the config) is required.");

            Directory.CreateDirectory(outputDir);

            var summary = new StringBuilder();
            summary.AppendLine($"ShaderStaticGate run {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
            summary.AppendLine($"Unity {Application.unityVersion}; platform target D3D11 / StandaloneWindows64");
            summary.AppendLine($"Config: {Path.GetFullPath(configPath)}");
            summary.AppendLine();

            var anyFailure = false;

            foreach (GateEntry entry in config.entries)
            {
                string status;

                try
                {
                    status = !string.IsNullOrEmpty(entry.computeAssetPath)
                        ? RunComputeEntry(entry, outputDir)
                        : RunShaderEntry(entry, outputDir);
                }
                catch (Exception ex)
                {
                    status = $"FAIL {ex.Message}";
                }

                if (status.StartsWith("FAIL", StringComparison.Ordinal))
                    anyFailure = true;

                summary.AppendLine($"{DescribeEntry(entry)}: {status}");
                Debug.Log($"[ShaderStaticGate] {DescribeEntry(entry)}: {status}");
            }

            File.WriteAllText(Path.Combine(outputDir, SUMMARY_FILE_NAME), summary.ToString());
            return anyFailure ? 1 : 0;
        }

        private static ShaderData.Pass? TryFindPass(Shader shader, string passName, out string diagnostics)
        {
            var report = new StringBuilder();
            report.Append("asset=").Append(AssetDatabase.GetAssetPath(shader));

            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    report.Append("; import-msg=").Append(message.severity).Append(':').Append(message.message);
            }

            ShaderData shaderData = ShaderUtil.GetShaderData(shader);
            report.Append("; subshaders=").Append(shaderData.SubshaderCount);
            ShaderData.Pass? found = null;

            for (var s = 0; s < shaderData.SubshaderCount; s++)
            {
                ShaderData.Subshader candidate = shaderData.GetSubshader(s);
                report.Append("; ss").Append(s).Append('=');

                for (var i = 0; i < candidate.PassCount; i++)
                {
                    ShaderData.Pass candidatePass = candidate.GetPass(i);
                    report.Append(i > 0 ? "|" : string.Empty).Append('\'').Append(candidatePass.Name).Append('\'');

                    if (found == null && string.Equals(candidatePass.Name, passName, StringComparison.OrdinalIgnoreCase))
                        found = candidatePass;
                }
            }

            diagnostics = report.ToString();
            return found;
        }

        private static string RunShaderEntry(GateEntry entry, string outputDir)
        {
            Shader shader = Shader.Find(entry.shaderName);

            if (shader == null)
                return $"FAIL Shader.Find returned null for '{entry.shaderName}'";

            ShaderData.Pass? pass = TryFindPass(shader, entry.passName, out string diagnostics);

            if (pass == null)
            {
                // A stale import (e.g. cached from a session with compile errors) can leave the
                // shader data with a single unnamed pass — force a reimport and retry once.
                string assetPath = AssetDatabase.GetAssetPath(shader);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                shader = Shader.Find(entry.shaderName);

                if (shader == null)
                    return $"FAIL Shader.Find returned null for '{entry.shaderName}' after reimport of '{assetPath}'";

                pass = TryFindPass(shader, entry.passName, out diagnostics);
            }

            if (pass == null)
                return $"FAIL pass '{entry.passName}' not found on '{entry.shaderName}' [{diagnostics}]";

            string[] keywords = entry.keywords ?? Array.Empty<string>();
            var failures = 0;


            foreach (ShaderType stage in STAGES)
            {
                ShaderData.VariantCompileInfo variant = pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);

                string baseName = VariantFileBaseName(entry, stage);
                string reportPath = Path.Combine(outputDir, baseName + ".txt");
                WriteVariantReport(reportPath, entry, stage, keywords, variant);

                if (variant.Success && variant.ShaderData is { Length: > 0 })
                    File.WriteAllBytes(Path.Combine(outputDir, baseName + ".dxbc.bin"), variant.ShaderData);
                else
                    failures++;
            }

            return failures == 0 ? "OK" : $"FAIL {failures}/{STAGES.Length} stages failed to compile (see per-variant reports)";
        }

        private static void WriteVariantReport(string reportPath, GateEntry entry, ShaderType stage, string[] keywords, ShaderData.VariantCompileInfo variant)
        {
            var report = new StringBuilder();
            report.AppendLine($"Shader:   {entry.shaderName}");
            report.AppendLine($"Pass:     {entry.passName}");
            report.AppendLine($"Stage:    {stage}");
            report.AppendLine($"Keywords: {(keywords.Length == 0 ? "<none>" : string.Join(" ", keywords))}");
            report.AppendLine("Platform: D3D11 (ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64)");
            report.AppendLine($"Success:  {variant.Success}");

            if (variant.Messages != null)
                foreach (ShaderMessage message in variant.Messages)
                    report.AppendLine($"Message [{message.severity}] {message.message} {message.messageDetails}");

            byte[] bytecode = variant.ShaderData;

            if (bytecode == null || bytecode.Length == 0)
            {
                report.AppendLine("Bytecode: <none>");
                File.WriteAllText(reportPath, report.ToString());
                return;
            }

            // The public API exposes compiled bytecode, not text disassembly: the byte size + STAT
            // instruction counts below are the diffable signal; the .dxbc.bin next to this report can
            // be disassembled externally with any DXBC tool for the full listing.
            report.AppendLine($"Bytecode: {bytecode.Length} bytes (raw DXBC written alongside as .dxbc.bin)");

            if (!TryAppendDxbcStats(bytecode, report))
                report.AppendLine("DXBC STAT chunk not found or unparsable; byte size above is the only size signal.");

            AppendBindings(report, variant);
            File.WriteAllText(reportPath, report.ToString());
        }

        private static void AppendBindings(StringBuilder report, ShaderData.VariantCompileInfo variant)
        {
            try
            {
                if (variant.ConstantBuffers != null)
                {
                    report.AppendLine($"ConstantBuffers: {variant.ConstantBuffers.Length}");

                    foreach (var constantBuffer in variant.ConstantBuffers)
                        report.AppendLine($"  cbuffer {constantBuffer.Name} size={constantBuffer.Size}");
                }

                if (variant.TextureBindings != null)
                {
                    report.AppendLine($"TextureBindings: {variant.TextureBindings.Length}");

                    foreach (var texture in variant.TextureBindings)
                        report.AppendLine($"  texture {texture.Name} index={texture.Index} dim={texture.Dim}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"Binding dump failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Parses the DXBC container and dumps the STAT chunk (d3d11 shader statistics, instruction
        ///     counts first) plus per-chunk sizes. Returns false when the blob is not a DXBC container.
        /// </summary>
        private static bool TryAppendDxbcStats(byte[] data, StringBuilder report)
        {
            try
            {
                if (data.Length < 36 || data[0] != (byte)'D' || data[1] != (byte)'X' || data[2] != (byte)'B' || data[3] != (byte)'C')
                    return false;

                uint chunkCount = BitConverter.ToUInt32(data, 28);

                if (chunkCount > 64)
                    return false;

                var foundStat = false;

                for (var i = 0; i < chunkCount; i++)
                {
                    var chunkOffset = (int)BitConverter.ToUInt32(data, 32 + (i * 4));

                    if (chunkOffset < 0 || chunkOffset + 8 > data.Length)
                        return false;

                    var fourCc = Encoding.ASCII.GetString(data, chunkOffset, 4);
                    uint chunkSize = BitConverter.ToUInt32(data, chunkOffset + 4);
                    report.AppendLine($"Chunk {fourCc}: {chunkSize} bytes");

                    if (fourCc != "STAT")
                        continue;

                    foundStat = true;
                    var dwordCount = (int)(chunkSize / 4);

                    for (var d = 0; d < dwordCount && chunkOffset + 8 + (d * 4) + 4 <= data.Length; d++)
                    {
                        uint value = BitConverter.ToUInt32(data, chunkOffset + 8 + (d * 4));
                        string label = STAT_LABELS.TryGetValue(d, out string? known) ? known : $"stat[{d}]";
                        report.AppendLine($"  STAT {label} = {value}");
                    }
                }

                return foundStat;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Compute shaders cannot go through ShaderData: falls back to reflection over the internal
        ///     ShaderUtil.OpenCompiledComputeShader (which writes Temp/Compiled-*.shader before opening it)
        ///     and copies whatever compiled dump appears in Temp/ into the output dir. Writes a SKIPPED
        ///     stub with the reason when the internal API is absent or produces nothing.
        /// </summary>
        private static string RunComputeEntry(GateEntry entry, string outputDir)
        {
            string stubPath = Path.Combine(outputDir, SanitizeFileName(Path.GetFileName(entry.computeAssetPath)) + ".txt");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(entry.computeAssetPath);

            if (compute == null)
            {
                File.WriteAllText(stubPath, $"SKIPPED: compute asset not found at '{entry.computeAssetPath}'.");
                return $"FAIL compute asset not found at '{entry.computeAssetPath}'";
            }

            MethodInfo? openCompiled = typeof(ShaderUtil).GetMethod("OpenCompiledComputeShader", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (openCompiled == null)
            {
                File.WriteAllText(stubPath, "SKIPPED: ShaderUtil.OpenCompiledComputeShader is not present in this Unity version; no public compute-compile API exists. Diff the source and use a live GPU capture instead.");
                return "SKIPPED internal OpenCompiledComputeShader not found";
            }

            string tempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
            DateTime invokeTime = DateTime.UtcNow.AddSeconds(-1);

            try
            {
                ParameterInfo[] parameters = openCompiled.GetParameters();
                var arguments = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;

                    if (typeof(ComputeShader).IsAssignableFrom(parameterType))
                        arguments[i] = compute;
                    else if (parameterType == typeof(bool))
                        arguments[i] = parameters[i].Name != null && parameters[i].Name!.StartsWith("allVariants", StringComparison.OrdinalIgnoreCase);
                    else if (parameterType == typeof(int))
                        arguments[i] = 0;
                    else
                        arguments[i] = null;
                }

                openCompiled.Invoke(null, arguments);
            }
            catch (Exception ex)
            {
                // Opening the dump in an external editor can fail in batchmode; the dump file itself is
                // written before the open attempt, so keep scanning Temp/ below.
                Debug.LogWarning($"[ShaderStaticGate] OpenCompiledComputeShader invocation threw ({ex.Message}); scanning Temp/ for the dump anyway.");
            }

            var copied = new List<string>();

            if (Directory.Exists(tempDir))
            {
                foreach (string file in Directory.GetFiles(tempDir, "Compiled-*"))
                {
                    if (File.GetLastWriteTimeUtc(file) < invokeTime)
                        continue;

                    string destination = Path.Combine(outputDir, SanitizeFileName(Path.GetFileName(entry.computeAssetPath)) + "__" + Path.GetFileName(file));
                    File.Copy(file, destination, true);
                    copied.Add(Path.GetFileName(destination));
                }
            }

            if (copied.Count == 0)
            {
                File.WriteAllText(stubPath, "SKIPPED: OpenCompiledComputeShader produced no Temp/Compiled-* dump (reflection fallback; kernel: " + entry.kernelName + ").");
                return "SKIPPED no compiled compute dump produced";
            }

            File.WriteAllText(stubPath, $"OK: compute dump(s) copied: {string.Join(", ", copied)} (kernel of interest: {entry.kernelName}).");
            return $"OK ({copied.Count} dump file(s))";
        }

        private static string DescribeEntry(GateEntry entry) =>
            !string.IsNullOrEmpty(entry.computeAssetPath)
                ? $"compute '{entry.computeAssetPath}' kernel '{entry.kernelName}'"
                : $"shader '{entry.shaderName}' pass '{entry.passName}' keywords [{string.Join(" ", entry.keywords ?? Array.Empty<string>())}]";

        private static string VariantFileBaseName(GateEntry entry, ShaderType stage)
        {
            string keywordPart = entry.keywords is { Length: > 0 } ? string.Join("-", entry.keywords) : "nokw";
            return SanitizeFileName($"{entry.shaderName}__{entry.passName}__{keywordPart}__{stage.ToString().ToLowerInvariant()}");
        }

        private static string SanitizeFileName(string name)
        {
            var builder = new StringBuilder(name.Length);

            foreach (char c in name)
                builder.Append(char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_');

            return builder.ToString();
        }

        private static string? GetArgValue(string argName)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], argName, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];

            return null;
        }

        // Config schema: scripts/plaza-bench/shader-gate-config.json (JsonUtility: keys must match field names).
        [Serializable]
        public class GateConfig
        {
            public string outputDir = string.Empty;
            public GateEntry[] entries = Array.Empty<GateEntry>();
        }

        [Serializable]
        public class GateEntry
        {
            public string shaderName = string.Empty;
            public string passName = string.Empty;
            public string[] keywords = Array.Empty<string>();
            public string computeAssetPath = string.Empty;
            public string kernelName = string.Empty;
        }
    }
}
