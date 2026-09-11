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
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
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
        public void UiToolkitFontAssetsMustNotBeStatic()
        {
            // The Advanced Text Generator, the default UI Toolkit text system since Unity 6.5, cannot render
            // static font assets: https://docs.unity3d.com/Manual/ui-systems/migrate-static-font-assets.html
            // The TextMesh Pro font assets are deliberately out of scope: uGUI text uses its own generator.
            const string FONTS_FOLDER = "Assets/DCL/UIToolkit/Fonts";

            var scanned = new List<string>();
            var offenders = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:FontAsset", new[] { FONTS_FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);

                if (fontAsset == null)
                    continue;

                scanned.Add(path);

                // DynamicOS resolves its face through the operating system, so it carries neither of the two below.
                if (fontAsset.atlasPopulationMode == AtlasPopulationMode.DynamicOS)
                    continue;

                // Compared against Dynamic rather than Static: Unity 6.5 deprecated AtlasPopulationMode.Static,
                // so naming it here would raise CS0618.
                if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                {
                    offenders.Add($"{path} :: atlas population mode is Static");
                    continue;
                }

                if (fontAsset.sourceFontFile == null)
                    offenders.Add($"{path} :: Dynamic, but without a source font file no glyph can be rasterized");

                if (fontAsset.atlasTexture != null && !fontAsset.atlasTexture.isReadable)
                    offenders.Add($"{path} :: Dynamic, but its atlas texture is not readable, so no glyph can be added to it");

                // Read through SerializedObject: only the serialized field name is stable across Unity versions.
                if (!new SerializedObject(fontAsset).FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue)
                    offenders.Add($"{path} :: Dynamic, but multi-atlas textures are off, so its atlas cannot grow past the first page");
            }

            Assert.That(scanned, Is.Not.Empty, $"No font asset was found in {FONTS_FOLDER}, so this test cannot pass on its own merit.");

            Assert.That(offenders, Is.Empty,
                "The Advanced Text Generator can only render font assets that populate their atlas dynamically:\n"
                + string.Join("\n", offenders));
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
