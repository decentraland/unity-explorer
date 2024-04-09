#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
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
        private static readonly string[] EXCLUDED_PATHS = { "/Editor/", "/Tests/", "/EditorTests/", "/Rendering/SkyBox/", "/Ipfs/", "/Plugins/SocketIO" };

        private static IEnumerable<string> AllCSharpFiles() =>
            AssetDatabase.FindAssets("t:Script")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(assetPath => Path.GetFileName(assetPath) != "AssemblyInfo.cs" && Path.GetExtension(assetPath) == ".cs" &&
                                             !assetPath.StartsWith("Packages/") && !EXCLUDED_PATHS.Any(assetPath.Contains));

        public void ClassShouldBeInNamespaces(string file)
        {
            // Arrange
            string fileContent = File.ReadAllText(file);
            SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

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


        public void AllAsyncMethodsShouldEndWithAsyncSuffix(string file)
        {
            // Arrange
            string fileContent = File.ReadAllText(file);

            if (fileContent.Contains("[IgnoreAsyncNaming"))
                return;

            SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

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
                $"File {Path.GetFileName(file)}: Found async methods/functions without 'Async' suffix: \n{string.Join("\n", methodsWithoutProperSuffix)}");
        }


        public void UsingUnityEditorShouldBeSurroundedByDirectives(string file)
        {
            string fileContent = File.ReadAllText(file);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
            SyntaxNode root = tree.GetRoot();

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
