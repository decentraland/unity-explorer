#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using System.Text.RegularExpressions;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests
{
    [Category(CODE_CONVENTIONS)]
    public class CodeConventionsTests
    {
        private const string TRUST_WEBGL_THREAD_SAFETY_FLAG = nameof(TRUST_WEBGL_THREAD_SAFETY_FLAG);
        private const string IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG = nameof(IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG);

        private const string TRUST_WEBGL_SYSTEM_TASKS_SAFETY_FLAG = nameof(TRUST_WEBGL_SYSTEM_TASKS_SAFETY_FLAG);
        private const string IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG = nameof(IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG);

        private const string THREADING_CLASSES_API_LIST_PATH = "Assets/DCL/Tests/Editor/excludes_threading.txt";
        
        private static readonly string[] UNITASK_FORBIDDEN_CALLS = new []
        {
            "UniTask.SwitchToThreadPool",
            "UniTask.RunOnThreadPool"
        };
        // TODO better regex matching?

        private static readonly string[] WEBGL_THREAD_SAFETY_EXCLUDED_PATHS = {
            "Assets/DCL/Input/UnityInputSystem/DCLInput.cs"
        }; // cause it's autogen

        private static readonly string[] WEB_SOCKETS_EXCLUDED_PATHS = {
            "Assets/DCL/Infrastructure/Utility/Networking/DCLWebSocket.cs"
        }; // cause it's autogen

        private static readonly string[] EXCLUDED_PATHS = { "/Editor/", "/Test", "/Playground", "/EditorTests/", "/Rendering/SkyBox/", "/Ipfs/", "/Plugins/SocketIO" };

        private static readonly string[] EXCLUDED_PATHS_INCLUDE_SOCKET_IO = { "/Editor/", "/Test", "/Playground", "/EditorTests/", "/Rendering/SkyBox/", "/Ipfs/" };


        private static IEnumerable<string> AllCSharpFiles() =>
            AssetDatabase.FindAssets("t:Script")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(assetPath => Path.GetFileName(assetPath) != "AssemblyInfo.cs" && Path.GetExtension(assetPath) == ".cs" &&
                                             !assetPath.StartsWith("Packages/") && !EXCLUDED_PATHS.Any(assetPath.Contains));


        private static IEnumerable<string> AllCSharpFilesWithSocketIO() =>
            AssetDatabase.FindAssets("t:Script")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(assetPath => Path.GetFileName(assetPath) != "AssemblyInfo.cs" && Path.GetExtension(assetPath) == ".cs" &&
                                             !assetPath.StartsWith("Packages/") && !EXCLUDED_PATHS_INCLUDE_SOCKET_IO.Any(assetPath.Contains));

        private static string[] THREADING_FORBIDDEN_CLASSES = null!;


        [SetUp]
        public void Init()
        {
            THREADING_FORBIDDEN_CLASSES = 
                File.ReadLines(THREADING_CLASSES_API_LIST_PATH)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToArray();
        }

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

        [TestCaseSource(nameof(AllCSharpFilesWithSocketIO))]
        public void VerifyShouldNotUseThreadingApiDirectly(string filePath)
        {
            if (WEBGL_THREAD_SAFETY_EXCLUDED_PATHS.Contains(filePath))
                return;

            string fileContent = File.ReadAllText(filePath);
            ShouldNotUseThreadingApiDirectly(fileContent, filePath);
        }

        [TestCaseSource(nameof(AllCSharpFilesWithSocketIO))]
        public void VerifyShouldNotUseDangerousUniTask(string filePath)
        {
            string fileContent = File.ReadAllText(filePath);
            ShouldNotUseDangerousUniTask(fileContent, filePath);
        }

        [TestCaseSource(nameof(AllCSharpFilesWithSocketIO))]
        public void VerifyShouldNotUseSystemTask(string filePath)
        {
            string fileContent = File.ReadAllText(filePath);
            ShouldNotUseSystemTask(fileContent, filePath);
        }

        [Test]
        public void VerifyShouldNotUseWaitForComplition()
        {
            // forbidden pattern
            const string pattern = @"\.GetLocalizedString\(\)";
            string projectRoot = Directory.GetCurrentDirectory();

            // Use rg because C# FileStream is very slow + avoid overhead of NUnit per file
            var psi = new ProcessStartInfo
            {
                FileName = "/opt/homebrew/bin/rg",
                Arguments = string.Join(" ", new[]
                        {
                        "--line-number",
                        "--no-heading",
                        "--color", "never",
                        $"\"{pattern}\"",
                        $"\"{projectRoot}/Assets\"",
                        "--glob", "\"*.cs\""
                        }),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                Assert.Fail("Failed to start ripgrep (rg). Is it installed and on PATH?");

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            // rg exit codes:
            // 0 = matches found
            // 1 = no matches
            // 2 = error
            if (process.ExitCode == 2)
            {
                Assert.Fail($"ripgrep error:\n{stderr}");
            }

            if (process.ExitCode == 0)
            {
                Assert.Fail(
                        "Detected forbidden API usage:\n\n" +
                        stdout +
                        "\nUse async version instead."
                        );
            }
        }

        [TestCaseSource(nameof(AllCSharpFilesWithSocketIO))]
        public void VerifyShouldNotUseNativeWebSocket(string filePath)
        {
            if (WEB_SOCKETS_EXCLUDED_PATHS.Contains(filePath))
                return;

            string fileContent = File.ReadAllText(filePath);
            ShouldNotUseNativeWebSocket(fileContent, filePath);
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

        // To support WebGL compatability
        private static void ShouldNotUseNativeWebSocket(string fileContent, string filePath)
        {
            //if (fileContent.Contains(TRUST_WEBGL_SYSTEM_TASKS_SAFETY_FLAG))
             //   return;

            var lines = fileContent.Split('\n');
            var violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                string pattern = "System.Net.WebSockets";
                if (line.Contains(pattern))
                        //&& line.Contains(IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG) == false)
                {
                    violations.Add($"{filePath}:{i + 1}: uses '{pattern}'");
                }
            }

            Assert.IsTrue(
                    violations.Count == 0,
                    violations.Count == 0 
                    ? string.Empty 
                    : $"File {Path.GetFileName(filePath)}: Detected forbidden API usage:\n{string.Join("\n", violations)}\nUse DCLWebSocket instead"
                    );
        }

        // To support WebGL compatability
        private static void ShouldNotUseSystemTask(string fileContent, string filePath)
        {
            if (fileContent.Contains(TRUST_WEBGL_SYSTEM_TASKS_SAFETY_FLAG))
                return;

            var lines = fileContent.Split('\n');
            var violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                string pattern = "System.Threading.Tasks";
                if (line.Contains(pattern)
                        && line.Contains(IGNORE_LINE_WEBGL_SYSTEM_TASKS_SAFETY_FLAG) == false)
                {
                    violations.Add($"{filePath}:{i + 1}: uses '{pattern}'");
                }
            }

            Assert.IsTrue(violations.Count == 0,
                    $"File {Path.GetFileName(filePath)}: Detected forbidden API usage:\n{string.Join("\n", violations)}\nUse DCLTask instead");
        }

        // To support WebGL compatability
        private static void ShouldNotUseDangerousUniTask(string fileContent, string filePath)
        {
            var lines = fileContent.Split('\n');
            var violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                foreach (string pattern in UNITASK_FORBIDDEN_CALLS)
                {
                    if (line.Contains(pattern))
                    {
                        violations.Add($"{filePath}:{i + 1}: uses '{pattern}'");
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                    $"File {Path.GetFileName(filePath)}: Detected forbidden API usage:\n{string.Join("\n", violations)}\nUse DCLTask instead");
        }

        // To support WebGL compatability
        private static void ShouldNotUseThreadingApiDirectly(string fileContent, string filePath)
        {
            var lines = fileContent.Split('\n');
            var violations = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Contains(TRUST_WEBGL_THREAD_SAFETY_FLAG))
                    break;

                if (line.Contains(IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG))
                    continue;

                // Ignore namespace keyword
                if (line.StartsWith("namespace")) 
                    continue;

                foreach (string forbiddenClass in THREADING_FORBIDDEN_CLASSES)
                {
                    var pattern = $@"\b{Regex.Escape(forbiddenClass)}\b";

                    if (Regex.IsMatch(line, pattern))
                    {
                        violations.Add($"{filePath}:{i + 1}: uses '{forbiddenClass}'");
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                    $"File {Path.GetFileName(filePath)}: Detected forbidden API usage:\n{string.Join("\n", violations)}\nIf it's intendent use TRUST_WEBGL_THREAD_SAFETY_FLAG or IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG");
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
