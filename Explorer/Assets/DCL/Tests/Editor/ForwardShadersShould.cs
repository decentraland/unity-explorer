using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests
{
    /// <summary>
    ///     URP 17 renders with the Forward+ clustered light loop, selected through the global keyword
    ///     _CLUSTER_LIGHT_LOOP. A lit forward shader that does not compile that variant falls back to the
    ///     per-object light path, which the Forward+ renderer never feeds, so every project shader with a
    ///     UniversalForward pass that evaluates real-time lights has to declare the keyword in that pass.
    /// </summary>
    [Category(CODE_CONVENTIONS)]
    public class ForwardShadersShould
    {
        private const string SHADER_ROOT = "Assets/DCL";
        private const string REQUIRED_PRAGMA = "#pragma multi_compile _ _CLUSTER_LIGHT_LOOP";
        private const int INCLUDE_DEPTH = 4;

        // Lit-looking forward shaders that do not declare the keyword, each with the reason the rule
        // does not apply to it. An entry that stops being an offender fails KeepTheExemptionListCurrent.
        private static readonly Dictionary<string, string> EXEMPT = new ()
        {
            ["Assets/DCL/UI/LoadingSpinner/S_LoadingSpinner.shader"] =
                "Shader Graph sprite passes (SHADERPASS_SPRITEUNLIT / SHADERPASS_SPRITEFORWARD): Lighting.hlsl arrives with the graph boilerplate and no light is evaluated",
            ["Assets/DCL/Landscape/Shaders/MountainLit.shader"] =
                "declares the deprecated _FORWARD_PLUS alias: URP still raises it at runtime next to _CLUSTER_LIGHT_LOOP and Core.hlsl maps it onto the cluster loop, so the terrain renders Forward+, but the variant stripper keys only on _CLUSTER_LIGHT_LOOP, so player builds can lose its additional-light-shadow variants until the pragma migrates",
        };

        private static readonly Regex FORWARD_TAG = new (@"""LightMode""\s*=\s*""UniversalForward""", RegexOptions.Compiled);
        private static readonly Regex REQUIRED_PRAGMA_LINE = new (@"^[ \t]*#pragma[ \t]+multi_compile[ \t]+_[ \t]+_CLUSTER_LIGHT_LOOP\b", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex REALTIME_LIGHTING = new (@"ShaderLibrary/(?:Lighting|RealtimeLights)\.hlsl""|\b(?:UniversalFragmentPBR|GetMainLight|GetAdditionalLight)\s*\(", RegexOptions.Compiled);
        private static readonly Regex LOCAL_INCLUDE = new (@"^[ \t]*#include(?:_with_pragmas)?[ \t]+""(?!Packages/)([^""]+)""", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex PASS_OPEN = new (@"\bPass\s*\{", RegexOptions.Compiled);
        private static readonly Regex HLSL_INCLUDE_BLOCK = new (@"HLSLINCLUDE(.*?)ENDHLSL", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex COMMENTS = new (@"/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline | RegexOptions.Compiled);

        [Test]
        public void DeclareTheClusterLightLoopKeywordInEveryLitForwardPass()
        {
            LitForwardShaders(out List<string> offenders);
            string[] unexpected = offenders.Where(path => !EXEMPT.ContainsKey(path)).ToArray();

            Assert.That(unexpected, Is.Empty,
                $"lit UniversalForward shaders without '{REQUIRED_PRAGMA}' in the forward pass:\n  {string.Join("\n  ", unexpected)}");
        }

        [Test]
        public void KeepTheExemptionListCurrent()
        {
            LitForwardShaders(out List<string> offenders);

            foreach (KeyValuePair<string, string> exemption in EXEMPT)
            {
                Assert.That(File.Exists(exemption.Key), Is.True, $"{exemption.Key} is exempt but no longer exists");
                Assert.That(offenders, Does.Contain(exemption.Key),
                    $"{exemption.Key} now declares the keyword or no longer evaluates lights; drop its exemption ({exemption.Value})");
            }
        }

        [Test]
        public void RecogniseTheLandscapeShadersThatEvaluateLights()
        {
            // guards the scan itself: a pattern that matched nothing would make the rule vacuously true
            List<string> lit = LitForwardShaders(out _);

            Assert.That(lit, Does.Contain("Assets/DCL/Landscape/Shaders/Rock/DCL_Rock.shader"));
            Assert.That(lit, Does.Contain("Assets/DCL/Landscape/Shaders/Tree/DCL_Tree.shader"), "lighting reached through a local include");
            Assert.That(lit, Does.Contain("Assets/DCL/Landscape/Shaders/Tree/TreeImposter/DCL_Tree_Impostor.shader"));
            Assert.That(lit, Does.Not.Contain("Assets/DCL/AvatarRendering/AvatarShape/Ghosts/GhostHologram.shader"), "an unlit forward pass is outside the rule");
        }

        private static List<string> LitForwardShaders(out List<string> offenders)
        {
            var lit = new List<string>();
            offenders = new List<string>();

            foreach (string path in ProjectShaders())
            {
                string source = StripComments(File.ReadAllText(path));
                List<(int start, int end)> forwardPasses = ForwardPassSpans(source);

                if (forwardPasses.Count == 0) continue;
                if (!UsesRealtimeLighting(path, source, new HashSet<string>(), INCLUDE_DEPTH)) continue;

                lit.Add(path);

                bool declaredForEveryPass = HLSL_INCLUDE_BLOCK.Matches(source).Cast<Match>().Any(block => REQUIRED_PRAGMA_LINE.IsMatch(block.Groups[1].Value));

                if (!declaredForEveryPass && !forwardPasses.All(span => REQUIRED_PRAGMA_LINE.IsMatch(source.Substring(span.start, span.end - span.start))))
                    offenders.Add(path);
            }

            return lit;
        }

        private static IEnumerable<string> ProjectShaders() =>
            AssetDatabase.FindAssets("t:Shader", new[] { SHADER_ROOT })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(path => path.EndsWith(".shader", StringComparison.Ordinal))
                         .Distinct()
                         .OrderBy(path => path, StringComparer.Ordinal);

        private static string StripComments(string source) =>
            COMMENTS.Replace(source, string.Empty);

        // Every Pass block carrying the UniversalForward tag; a tag outside any Pass block widens the
        // span to the whole file so the pragma is still required somewhere.
        private static List<(int start, int end)> ForwardPassSpans(string source)
        {
            var spans = new List<(int start, int end)>();
            List<Match> passOpens = PASS_OPEN.Matches(source).Cast<Match>().ToList();

            foreach (Match tag in FORWARD_TAG.Matches(source))
            {
                Match open = passOpens.LastOrDefault(m => m.Index < tag.Index);
                int braceIndex = open == null ? -1 : open.Index + open.Length - 1;
                int end = braceIndex < 0 ? -1 : MatchingBrace(source, braceIndex);

                spans.Add(end > tag.Index ? (braceIndex, end) : (0, source.Length));
            }

            return spans;
        }

        private static int MatchingBrace(string source, int openIndex)
        {
            var depth = 0;

            for (int i = openIndex; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return i + 1;
            }

            return -1;
        }

        private static bool UsesRealtimeLighting(string path, string source, HashSet<string> visited, int depth)
        {
            if (REALTIME_LIGHTING.IsMatch(source)) return true;
            if (depth == 0) return false;

            string directory = Path.GetDirectoryName(path) ?? string.Empty;

            foreach (Match include in LOCAL_INCLUDE.Matches(source))
            {
                string includePath = Path.GetFullPath(Path.Combine(directory, include.Groups[1].Value));

                if (!File.Exists(includePath) || !visited.Add(includePath)) continue;
                if (UsesRealtimeLighting(includePath, StripComments(File.ReadAllText(includePath)), visited, depth - 1)) return true;
            }

            return false;
        }
    }
}
