using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests.Editor
{
    [Category(VALIDATION)]
    public class ValidationTests
    {
        private static readonly HashSet<string> DEBUG_METHOD_NAMES = new () { "Log", "LogError", "LogWarning", "LogException" };

        private readonly string[] excludedFolders = { "Editor" };
        private readonly string[] excludedFileNames = { "JsonUtils.cs", "WorldSyncCommandBufferCollectionsPool.cs" };
        private readonly string[] fileNameExclusionKeywords = { "Test", "Sentry" };

        [Test]
        public void ProjectShouldNotContainEmptyFolders()
        {
            // Arrange
            string[] allDirectories = Directory.GetDirectories(Application.dataPath, "*", SearchOption.AllDirectories);
            string excludedDirectory = Path.Combine(Application.dataPath, "AddressableAssetsData");
            allDirectories = allDirectories.Where(directory => !directory.StartsWith(excludedDirectory, StringComparison.OrdinalIgnoreCase)).ToArray();

            // Act
            var emptyDirectories = allDirectories.Where(IsDirectoryEmpty).ToList();
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
                    var fileName = Path.GetFileName(file);
                    var parts = file.Split(Path.DirectorySeparatorChar);

                    var isFolderExcluded = excludedFolders.Any(folder => parts.Contains(folder));
                    var isFileNameExcluded = fileNameExclusionKeywords.Any(keyword => fileName.Contains(keyword)) ||
                                             excludedFileNames.Contains(fileName);

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
            var scriptableObjects = GetAllScriptableObjectsInFolder("Assets/DCL");

            foreach (var scriptableObject in scriptableObjects)
            {
                if (!SerializationUtility.HasManagedReferencesWithMissingTypes(scriptableObject))
                    continue;

                var missingTypes = SerializationUtility.GetManagedReferencesWithMissingTypes(scriptableObject);

                var report = new StringBuilder();
                var missingClasses = new HashSet<string>();

                foreach (var missingType in missingTypes)
                    missingClasses.Add(MissingClassFullName(missingType));

                foreach (var missingClass in missingClasses)
                    report.Append("\t").Append(missingClass).AppendLine();

                Assert.Fail(
                    $"Missing references found in the following ScriptableObjects:\n{string.Join("\n", scriptableObject)}, {report}");
            }
        }

        private static string MissingClassFullName(ManagedReferenceMissingType missingType)
        {
            var description = new StringBuilder();

            if (missingType.namespaceName.Length > 0)
                description.Append(missingType.namespaceName).Append(".");

            description.AppendFormat("{0}, {1}", missingType.className, missingType.assemblyName);
            return description.ToString();
        }

        private static IEnumerable<ScriptableObject> GetAllScriptableObjectsInFolder(string folderPath)
        {
            return AssetDatabase.FindAssets("t:Object", new[] { folderPath })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
        }

        private static bool IsDirectoryEmpty(string path) =>
            !Directory.EnumerateFileSystemEntries(path).Any();
    }
}
