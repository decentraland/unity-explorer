using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace DCL.Tests
{
    [Category("CodeConventions")]
    public class CodeConventionsTests
    {
        private static readonly string[] EXCLUDED_PATHS = { "/Editor/", "/Tests/", "/EditorTests/", "/Rendering/SkyBox/", "/Ipfs/" };

        private static IEnumerable<string> AllCSharpFiles() =>
            AssetDatabase.FindAssets("t:Script")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(assetPath => Path.GetFileName(assetPath) != "AssemblyInfo.cs" && Path.GetExtension(assetPath) == ".cs" &&
                                             !assetPath.StartsWith("Packages/") && !EXCLUDED_PATHS.Any(assetPath.Contains));

        [TestCaseSource(nameof(AllCSharpFiles))]
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
                                                    !classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().Any() &&
                                                    classDeclaration.Parent is CompilationUnitSyntax)
                                               .ToList();

            // Assert
            Assert.AreEqual(0, classesOutsideNamespaces.Count,
                $"File {Path.GetFileName(file)}: Found {classesOutsideNamespaces.Count} non-partial classes outside of namespaces. All non-partial classes should be within a namespace.");
        }

        [TestCaseSource(nameof(AllCSharpFiles))]
        public void AllAsyncMethodsShouldEndWithAsyncSuffix(string file)
        {
            // Arrange
            string fileContent = File.ReadAllText(file);
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
