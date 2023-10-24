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
    public class EcsOrderingTests
    {
        private readonly string[] includePath = { "Assets/DCL/", "Assets/Scripts/ECS" };

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

                        systemToGroupMap.TryGetValue(targetSystemName, out string targetSystemGroup);

                        if (currentSystemGroup != targetSystemGroup)
                            errors.Add($"System {systemClass.Identifier.Text} in file {file} in group {currentSystemGroup} cannot be updated {orderAttribute.Name} system {targetSystemName} in group {targetSystemGroup}.");
                    }
                }
            }

            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        private static Dictionary<string, string> BuildSystemToGroupMap(IEnumerable<string> allCsFiles)
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
                        map[systemClass.Identifier.Text] = groupName;
                }
            }

            return map;
        }

        private static string GetTypeNameFromAttribute(AttributeSyntax attribute)
        {
            if (attribute == null) return null;
            if (attribute.ArgumentList.Arguments.Count <= 0) return null;

            if (attribute.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax argument)
                return argument.Type.ToString();

            return null;
        }

        private static bool IsCodeGenerated(string filePath) =>
            filePath.EndsWith(".g.cs") || filePath.EndsWith(".gen.cs") || filePath.Contains("Generated");
    }
}
