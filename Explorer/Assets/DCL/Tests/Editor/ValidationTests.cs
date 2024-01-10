using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests.Editor
{
    public class ValidationTests
    {
        private static readonly HashSet<string> DEBUG_METHOD_NAMES = new () { "Log", "LogError", "LogWarning", "LogException" };

        [Category(VALIDATION)]
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
            var sourceFiles = allSourceFiles.Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part.Contains("Test") || part.Contains("Sentry"))).ToList();

            foreach (string file in sourceFiles)
            {
                if (Path.GetFileName(file).Equals("JsonUtils.cs")) continue;
                if (Path.GetFileName(file).Equals("WorldSyncCommandBufferCollectionsPool.cs")) continue;

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

        private static bool IsDirectoryEmpty(string path) =>
            !Directory.EnumerateFileSystemEntries(path).Any();
    }
}
