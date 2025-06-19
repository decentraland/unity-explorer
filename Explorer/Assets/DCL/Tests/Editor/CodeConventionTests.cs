#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests
{
    [Category(CODE_CONVENTIONS)]
    public class CodeConventionsTests
    {
        private static readonly string[] EXCLUDED_PATHS = { "/Editor/", "/Test", "/Playground", "/EditorTests/", "/Rendering/SkyBox/", "/Ipfs/", "/Plugins/SocketIO" };

        private static IEnumerable<string> AllCSharpFiles() =>
            AssetDatabase.FindAssets("t:Script")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(assetPath => Path.GetFileName(assetPath) != "AssemblyInfo.cs" && Path.GetExtension(assetPath) == ".cs" &&
                                             !assetPath.StartsWith("Packages/") && !EXCLUDED_PATHS.Any(assetPath.Contains));

        [TestCaseSource(nameof(AllCSharpFiles))]
        public void VerifyConventions(string filePath)
        {
            // Arrange
            string fileContent = File.ReadAllText(filePath);
            SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

            ClassShouldBeInNamespaces(root, filePath);
            ShouldNotUsePlayerPrefsDirectly(fileContent, filePath);
            AllAsyncMethodsShouldEndWithAsyncSuffix(root, fileContent, filePath);
            UsingUnityEditorShouldBeSurroundedByDirectives(root, filePath);
        }

        private static void ClassShouldBeInNamespaces(SyntaxNode root, string file)
        {
            // Act
            var classesOutsideNamespaces = root.DescendantNodesAndSelf()
                                               .OfType<ClassDeclarationSyntax>()
                                               .Where(classDeclaration =>
                                                    !classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                                    !classDeclaration.Ancestors().Any(e => e is NamespaceDeclarationSyntax or CompilationUnitSyntax) &&
                                                    classDeclaration.Parent is CompilationUnitSyntax)
                                               .ToList();

            // Assert
            Assert.AreEqual(0, classesOutsideNamespaces.Count,
                $"File {Path.GetFileName(file)}: Found {classesOutsideNamespaces.Count} non-partial classes outside of namespaces. All non-partial classes should be within a namespace.");
        }

        private static void ShouldNotUsePlayerPrefsDirectly(string fileContent, string filePath)
        {
            // Ignore prefs plugin as it uses PlayerPrefs intentionally
            if (filePath.StartsWith("Assets/DCL/Prefs/")) return;

            string[]? lines = fileContent.Split('\n');
            var violations = new List<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int index = line.IndexOf("PlayerPrefs.", StringComparison.Ordinal);

                while (index != -1)
                {
                    bool isDclPrefixed = index >= 3 && line.Substring(index - 3, 3) == "DCL";

                    if (!isDclPrefixed)
                        violations.Add($"Line {i + 1}: {line.Trim()}");

                    index = line.IndexOf("PlayerPrefs.", index + 1, StringComparison.Ordinal);
                }
            }

            // Assert
            Assert.IsTrue(violations.Count == 0,
                $"File {Path.GetFileName(filePath)}: Detected direct use of 'PlayerPrefs.':\n{string.Join("\n", violations)}");
        }

        private static void AllAsyncMethodsShouldEndWithAsyncSuffix(SyntaxNode root, string fileContent, string filePath)
        {
            if (fileContent.Contains("[IgnoreAsyncNaming"))
                return;

            var asyncMethods = root.DescendantNodesAndSelf()
                                   .Where(n => (n is MethodDeclarationSyntax m && m.Modifiers.Any(SyntaxKind.AsyncKeyword)) ||
                                               (n is LocalFunctionStatementSyntax l && l.Modifiers.Any(SyntaxKind.AsyncKeyword)))
                                   .ToList();

            // Act
            var methodsWithoutProperSuffix = asyncMethods
                                            .Where(n => !GetName(n).EndsWith("Async"))
                                            .Select(n => $"{GetName(n)} (line {GetLineNumber(n)})")
                                            .ToList();

            // Assert
            Assert.AreEqual(0, methodsWithoutProperSuffix.Count,
                $"File {Path.GetFileName(filePath)}: Found async methods/functions without 'Async' suffix: \n{string.Join("\n", methodsWithoutProperSuffix)}");
        }

        private static void UsingUnityEditorShouldBeSurroundedByDirectives(SyntaxNode root, string file)
        {
            // Find all using directives for UnityEditor.
            var usingUnityEditorDirectives = root.DescendantNodes(descendIntoTrivia: true) // descendIntoTrivia to get preprocessor directives
                                                 .OfType<UsingDirectiveSyntax>()
                                                 .Where(u => u.Name.ToFullString().Trim() == "UnityEditor")
                                                 .ToList();

            foreach (UsingDirectiveSyntax usingDirective in usingUnityEditorDirectives)
            {
                var precedingTrivia = usingDirective.GetLeadingTrivia().ToList();
                var followingTrivia = usingDirective.GetTrailingTrivia().ToList();

                bool hasStartDirective = precedingTrivia.Any(t => t.IsKind(SyntaxKind.IfDirectiveTrivia) && t.ToFullString().Contains("UNITY_EDITOR"));
                bool hasEndDirective = followingTrivia.Any(t => t.IsKind(SyntaxKind.EndIfDirectiveTrivia));

                Assert.IsTrue(hasStartDirective, $"File {Path.GetFileName(file)}: 'using UnityEditor;' is not preceded by '#if UNITY_EDITOR'.");
                Assert.IsTrue(hasEndDirective, $"File {Path.GetFileName(file)}: 'using UnityEditor;' is not followed by '#endif'.");
            }
        }

        private static string GetName(SyntaxNode node) =>
            node switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text,
                LocalFunctionStatementSyntax localFunction => localFunction.Identifier.Text,
                _ => string.Empty,
            };

        private static int GetLineNumber(SyntaxNode node) =>
            node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
