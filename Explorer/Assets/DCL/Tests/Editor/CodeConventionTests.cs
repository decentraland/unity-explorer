using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

        [TestCaseSource(nameof(AllCSharpFiles))]
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

    public class ECSOrderingTests
    {
        private readonly string[] includePath = { "Assets/DCL/", "Assets/Scripts/ECS" };

        [Test]
        public void SystemGroupDerivedClassesShouldHaveGroupSuffix()
        {
            string[] allCsFiles = AssetDatabase.FindAssets("t:Script", includePath)
                                               .Select(AssetDatabase.GUIDToAssetPath)
                                               .Where(file => !IsCodeGenerated(file))
                                               .ToArray();

            foreach (string file in allCsFiles)
            {
                string fileContent = File.ReadAllText(file);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
                SyntaxNode root = tree.GetRoot();

                // Retrieve all class declarations
                IEnumerable<ClassDeclarationSyntax> classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (ClassDeclarationSyntax classDeclaration in classDeclarations)
                {
                    var baseType = classDeclaration.BaseList?.Types.FirstOrDefault()?.Type.ToString();

                    // Check if the class inherits from SystemGroup (or its descendants)
                    // Note: This check is simple and assumes that no other classes have the same name as SystemGroup or its descendants.
                    // For a more accurate check, consider using Roslyn's semantic model (which will be more expensive).
                    if (baseType == "SystemGroup" || baseType?.EndsWith("Group") == true) // You can adjust this check based on your project's structure
                    {
                        // Check if the class name has the suffix "Group"
                        if (!classDeclaration.Identifier.Text.EndsWith("Group")) { Assert.Fail($"Class {classDeclaration.Identifier.Text} in file {Path.GetFileName(file)} inherits from {baseType} but doesn't have the 'Group' suffix."); }
                    }
                }
            }
        }

        [Test]
        public void CheckSystemOrderingConsistency()
        {
            string[] allCsFiles = AssetDatabase.FindAssets("t:Script", includePath)
                                               .Select(AssetDatabase.GUIDToAssetPath)
                                               .Where(file => !IsCodeGenerated(file))
                                               .ToArray();

            Dictionary<string, string> systemToGroupMap = BuildSystemToGroupMap(allCsFiles);

            var errors = new List<string>();

            foreach (string file in allCsFiles)
            {
                string fileContent = File.ReadAllText(file);
                SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

                // Retrieve all class declarations.
                IEnumerable<ClassDeclarationSyntax> systemClasses = root.DescendantNodes()
                                                                        .OfType<ClassDeclarationSyntax>();

                foreach (ClassDeclarationSyntax systemClass in systemClasses)
                {
                    var attributes = systemClass.AttributeLists.SelectMany(attrList => attrList.Attributes).ToList();

                    // Get the group of the current system from the cached map.
                    systemToGroupMap.TryGetValue(systemClass.Identifier.Text, out string currentSystemGroup);

                    // Check for inconsistencies in UpdateAfter and UpdateBefore attributes.
                    var orderAttributes = attributes.Where(attr => attr.Name.ToString() == "UpdateAfter" || attr.Name.ToString() == "UpdateBefore").ToList();

                    foreach (AttributeSyntax orderAttribute in orderAttributes)
                    {
                        var targetSystemExpression = orderAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression as TypeOfExpressionSyntax;
                        var targetSystemName = targetSystemExpression?.Type.ToString();

                        if (targetSystemName.EndsWith("Group"))
                            continue;

                        systemToGroupMap.TryGetValue(targetSystemName, out string targetSystemGroup);

                        if (currentSystemGroup != targetSystemGroup)
                            errors.Add($"System {systemClass.Identifier.Text} in file {file} in group {currentSystemGroup} cannot be updated {orderAttribute.Name} system {targetSystemName} in group {targetSystemGroup}.");
                    }
                }
            }

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        private Dictionary<string, string> BuildSystemToGroupMap(string[] allCsFiles)
        {
            var map = new Dictionary<string, string>();

            foreach (string file in allCsFiles)
            {
                string fileContent = File.ReadAllText(file);
                SyntaxNode root = CSharpSyntaxTree.ParseText(fileContent).GetRoot();

                // Retrieve all class declarations.
                IEnumerable<ClassDeclarationSyntax> systemClasses = root.DescendantNodes()
                                                                        .OfType<ClassDeclarationSyntax>();

                foreach (ClassDeclarationSyntax systemClass in systemClasses)
                {
                    var attributes = systemClass.AttributeLists.SelectMany(attrList => attrList.Attributes).ToList();
                    string groupName = GetTypeNameFromAttribute(attributes.FirstOrDefault(attr => attr.Name.ToString() == "UpdateInGroup"));

                    if (groupName != null)
                    {
                        map[systemClass.Identifier.Text] = groupName;
                        Debug.Log($"System: {systemClass.Identifier.Text}, Group: {groupName}");
                    }
                    else { Debug.LogWarning($"No group found for system: {systemClass.Identifier.Text}"); }
                }
            }

            return map;
        }

        private string GetTypeNameFromAttribute(AttributeSyntax attribute)
        {
            if (attribute == null) return null;

            if (attribute.ArgumentList.Arguments.Count > 0)
            {
                var argument = attribute.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;

                if (argument != null) { return argument.Type.ToString(); }
            }

            return null;
        }

        private static bool IsCodeGenerated(string filePath) =>
            filePath.EndsWith(".g.cs") || filePath.EndsWith(".gen.cs") || filePath.Contains("Generated");
    }
}
