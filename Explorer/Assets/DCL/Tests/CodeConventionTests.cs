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
            string fileContent = File.ReadAllText(file);
            SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

            // Retrieve all non-partial class declarations that are direct children of the root (CompilationUnitSyntax).
            var classesOutsideNamespaces = root.DescendantNodesAndSelf()
                                               .OfType<ClassDeclarationSyntax>()
                                               .Where(classDeclaration =>
                                                    !classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                                    !classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().Any() &&
                                                    classDeclaration.Parent is CompilationUnitSyntax)
                                               .ToList();

            // Assert that there are no non-partial class declarations outside of namespaces.
            Assert.AreEqual(0, classesOutsideNamespaces.Count,
                $"File {Path.GetFileName(file)}: Found {classesOutsideNamespaces.Count} non-partial classes outside of namespaces. All non-partial classes should be within a namespace.");
        }
    }
}
