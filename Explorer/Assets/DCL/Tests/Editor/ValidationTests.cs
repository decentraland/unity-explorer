using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Utilities.Extensions;
using Global.Dynamic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using static Utility.Tests.TestsCategories;
using Object = UnityEngine.Object;

namespace DCL.Tests.Editor
{
    [Category(VALIDATION)]
    public class ValidationTests
    {
        private static readonly HashSet<string> DEBUG_METHOD_NAMES = new () { "Log", "LogError", "LogWarning", "LogException" };

        private readonly string[] excludedFolders = { "Editor", "Stylized Grass Shader", "UUAV" };
        private readonly string[] excludedFileNames = { "JsonUtils.cs", "WorldSyncCommandBufferCollectionsPool.cs", "DCLPlayerPrefs.cs" };
        private readonly string[] fileNameExclusionKeywords = { "Playground", "Test", "Sentry" };

        private readonly IReadOnlyCollection<string> pathIgnores = new List<string>
        {
            "node_modules",
            "dist",
            "sign-server",
            "textures-server",
            "simde",
            "cube-wave-16x16"
        };

        [Test]
        public void ProjectShouldNotContainEmptyFolders()
        {
            // Arrange
            string[] allDirectories = Directory.GetDirectories(Application.dataPath!, "*", SearchOption.AllDirectories);
            string excludedDirectory = Path.Combine(Application.dataPath, "AddressableAssetsData");

            allDirectories = allDirectories.Where(directory =>
                                                !directory.StartsWith(excludedDirectory, StringComparison.OrdinalIgnoreCase)
                                                && !directory.Contains("_SceneContext", StringComparison.OrdinalIgnoreCase))
                                           .ToArray();

            // Act
            var emptyDirectories = allDirectories
                                  .Where(IsDirectoryEmpty)
                                  .Where(p => PathInIgnore(p) == false)
                                  .ToList();

            string errorMessage = "Found empty directories:\n" + string.Join("\n", emptyDirectories);

            // Assert
            Assert.That(emptyDirectories.Count, Is.EqualTo(0), errorMessage);
        }

        [Test]
        public void CheckForDebugUsage()
        {
            string[] allSourceFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            var sourceFiles = allSourceFiles
                             .Where(file =>
                              {
                                  string fileName = Path.GetFileName(file);
                                  string[] parts = file.Split(Path.DirectorySeparatorChar);

                                  bool isFolderExcluded = excludedFolders.Any(folder => parts.Contains(folder));
                                  bool isFileNameExcluded = fileNameExclusionKeywords.Any(keyword => fileName.Contains(keyword)) || excludedFileNames.Contains(fileName);

                                  return !isFolderExcluded && !isFileNameExcluded;
                              })
                             .ToList();

            foreach (string file in sourceFiles)
            {
                string code = File.ReadAllText(file);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
                SyntaxNode root = syntaxTree.GetRoot();

                // Skip classes with names ending in 'Should', 'Test', or 'Tests'
                bool containsExcludedClass = root.DescendantNodes()
                                                 .OfType<ClassDeclarationSyntax>()
                                                 .Any(c => c.Identifier.ValueText.EndsWith("Should") ||
                                                           c.Identifier.ValueText.EndsWith("Test") ||
                                                           c.Identifier.ValueText.EndsWith("Tests"));

                if (containsExcludedClass) continue;

                IEnumerable<InvocationExpressionSyntax> debugLogStatements = root.DescendantNodes()
                                                                                 .OfType<InvocationExpressionSyntax>()
                                                                                 .Where(ies => ies.Expression is MemberAccessExpressionSyntax maes &&
                                                                                               maes.Expression.ToString() == "Debug" &&
                                                                                               DEBUG_METHOD_NAMES.Contains(maes.Name.Identifier.ValueText));

                Assert.IsEmpty(debugLogStatements, $"Debug usage found in file: {file}");
            }
        }

        [Test]
        public void CheckUnityObjectsForMissingReferences()
        {
            IEnumerable<ScriptableObject> scriptableObjects = GetAllScriptableObjectsInFolder("Assets/DCL");

            foreach (ScriptableObject scriptableObject in scriptableObjects)
            {
                if (!SerializationUtility.HasManagedReferencesWithMissingTypes(scriptableObject))
                    continue;

                ManagedReferenceMissingType[] missingTypes = SerializationUtility.GetManagedReferencesWithMissingTypes(scriptableObject);

                var report = new StringBuilder();
                var missingClasses = new HashSet<string>();

                foreach (ManagedReferenceMissingType missingType in missingTypes)
                    missingClasses.Add(MissingClassFullName(missingType));

                foreach (string missingClass in missingClasses)
                    report.Append("\t").Append(missingClass).AppendLine();

                Assert.Fail($"Missing references found in the following ScriptableObjects:\n{string.Join("\n", scriptableObject)}, {report}");
            }
        }

        [Test]
        public void ContextualImagePrefabsMustNotBakeSprite()
        {
            var offenders = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DCL" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                foreach (ContextualImage contextualImage in prefab.GetComponentsInChildren<ContextualImage>(true))
                {
                    Component image = contextualImage.GetComponent("Image");

                    if (image == null)
                        continue;

                    SerializedProperty sprite = new SerializedObject(image).FindProperty("m_Sprite");

                    if (sprite != null && sprite.objectReferenceValue != null)
                        offenders.Add($"{path} :: {contextualImage.name}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "ContextualImage requires an Image with no baked sprite; a baked sprite hard-links it into memory and defeats contextual loading:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void GpuiShaderBindingsCanonicalRegistryMustBePopulated()
        {
            const string CANONICAL_PATH = "Assets/DCL/Landscape/Assets/GPUI/GPUIShaderBindings.asset";

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CANONICAL_PATH);

            Assert.That(asset, Is.Not.Null,
                $"{CANONICAL_PATH} is missing — GPUI-rendered terrain assets (rocks/trees) would render magenta.");

            SerializedProperty? shaderInstances = new SerializedObject(asset).FindProperty("shaderInstances");

            Assert.That(shaderInstances, Is.Not.Null,
                $"{CANONICAL_PATH} is missing the 'shaderInstances' property — asset may be corrupt.");

            // Null ruled out by the assertion above.
            Assert.That(shaderInstances!.arraySize, Is.GreaterThan(0),
                $"{CANONICAL_PATH} has no shader bindings — GPUI-rendered terrain assets (rocks/trees) would render magenta.");
        }

        [Test]
        public void GpuiShaderBindingsShadowRegistryMustNotBeCommitted()
        {
            // GPUI Pro auto-creates a registry at its default Resources path during editor
            // sessions (this very test run may create one), so its presence on disk is
            // expected and harmless. Committing it is not: a committed (empty) copy shadows
            // the canonical registry in every checkout and terrain rocks/trees render
            // magenta. The path is gitignored; this guards against a forced re-add.
            const string SHADOW_PATH = "Assets/GPUInstancerPro/Resources/GPUIShaderBindings.asset";

            using var git = new Process();

            git.StartInfo = new ProcessStartInfo("git", $"ls-files --error-unmatch -- \"{SHADOW_PATH}\"")
            {
                // dataPath is an absolute path, so it always has a parent (the project root).
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try { git.Start(); }
            catch (Exception e) { Assert.Ignore($"git unavailable — cannot verify the tracked state of {SHADOW_PATH}: {e.Message}"); }

            git.WaitForExit();

            // Exit code 0 means git tracks the file; non-zero covers both "not tracked"
            // and environments without a usable repo (fail-open by design).
            Assert.That(git.ExitCode, Is.Not.Zero,
                $"{SHADOW_PATH} is committed — it shadows the canonical registry "
                + "(Assets/DCL/Landscape/Assets/GPUI/GPUIShaderBindings.asset) in every checkout and terrain "
                + "rocks/trees render magenta. Remove it from git (git rm --cached); the local file may stay.");
        }

        [Test]
        public void ProfileNameEditorWorldSizeLimitsLinkIsValid()
        {
            string prefab = File.ReadAllText(Path.Combine(Application.dataPath, "DCL/UI/Profiles/Names/Assets/ProfileNameEditor.prefab"));

            Assert.That(prefab, Does.Not.Contain("worlds/about/#worlds-size-limit"));
            Assert.That(prefab, Does.Contain("creator/scenes-sdk7/kinds-of-projects/kinds-of-project#size-limits"));
        }

        [UnityTest]
        public IEnumerator SettingsAreValid()
        {
            const string MAIN_SCENE = "Assets/Scenes/Main.unity";
            EditorSceneManager.OpenScene(MAIN_SCENE);
            MainSceneLoader boot = Object.FindAnyObjectByType<MainSceneLoader>().EnsureNotNull("Boot not found!");
            yield return boot.ValidateSettingsAsync().ToCoroutine();
        }

        private static string MissingClassFullName(ManagedReferenceMissingType missingType)
        {
            var description = new StringBuilder();

            if (missingType.namespaceName.Length > 0)
                description.Append(missingType.namespaceName).Append(".");

            description.AppendFormat("{0}, {1}", missingType.className, missingType.assemblyName);
            return description.ToString();
        }

        private static IEnumerable<ScriptableObject> GetAllScriptableObjectsInFolder(string folderPath) =>
            AssetDatabase.FindAssets("t:Object", new[] { folderPath })
                         .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)));

        private static bool IsDirectoryEmpty(string path) =>
            !Directory.EnumerateFileSystemEntries(path).Any();

        private bool PathInIgnore(string path)
        {
            foreach (string ignore in pathIgnores)
            {
                if (path.Contains(ignore))
                    return true;
            }

            return false;
        }
    }
}
