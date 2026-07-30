#if MCP_TEST_AUTOMATION
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Covers how <c>call_static_method</c> resolves a type, picks an overload and binds its arguments — the only
    ///     tool that reaches arbitrary code, and the one whose every refusal has to name what was wrong: a caller who
    ///     reads "no overload taking 1 parameter(s)" fixes the call, a caller who reads a stack trace does not.
    /// </summary>
    public class CallStaticMethodToolShould
    {
        /// <summary>The full, namespace-qualified name the tool resolves by; ToString spells it without a nullable FullName.</summary>
        private static readonly string PROBE = typeof(Probe).ToString();

        private CallStaticMethodTool tool = null!;

        [SetUp]
        public void SetUp()
        {
            tool = new CallStaticMethodTool();
        }

        [Test]
        public void InvokeTheResolvedMethodAndReportWhatItReturned()
        {
            // Act
            McpToolResult result = Call(PROBE, nameof(Probe.Add), new JArray { 2, 3 });

            // Assert
            Assert.That(result.Payload.ContainsKey("isError"), Is.False);

            JObject answer = JObject.Parse(TextOf(result));
            Assert.That(answer["type"]!.Value<string>(), Is.EqualTo(PROBE));
            Assert.That(answer["method"]!.Value<string>(), Is.EqualTo(nameof(Probe.Add)));
            Assert.That(answer["returnType"]!.Value<string>(), Is.EqualTo("Int32"));
            Assert.That(answer["value"]!.Value<int>(), Is.EqualTo(5));
        }

        [Test]
        public void NameTheTypeItCouldNotFind()
        {
            // Assert — an unloaded type, and a loaded one looked for in the wrong assembly.
            AssertRefused(Call("DCL.McpServer.Tests.NotAType", nameof(Probe.Add)), "DCL.McpServer.Tests.NotAType");
            AssertRefused(Call(PROBE, nameof(Probe.Add), new JArray { 2, 3 }, "No.Such.Assembly"), "No.Such.Assembly");
        }

        [Test]
        public void RefuseAGenericMethodDefinition()
        {
            // A generic definition cannot be invoked without its type arguments, which the wire format cannot carry.
            AssertRefused(Call(PROBE, nameof(Probe.Echo), new JArray { "x" }), nameof(Probe.Echo));
        }

        [Test]
        public void SayWhichArityItDoesTakeWhenNoOverloadMatches()
        {
            // Assert — the overload exists but takes two arguments, so the count it does take is the useful half.
            AssertRefused(Call(PROBE, nameof(Probe.Describe), new JArray { "label" }), nameof(Probe.Describe), "2");

            // Assert — nothing of that name at all is a different message, and must not claim an arity.
            AssertRefused(Call(PROBE, "Absent"), "Absent", "exposes no method");
        }

        [Test]
        public void RefuseAMethodThatDidNotOptIn()
        {
            // Act — a public static method that would resolve on name and arity, but carries no [McpCallable].
            McpToolResult result = Call(PROBE, nameof(Probe.Unmarked));

            // Assert — the refusal is the same one an absent method gets, so a caller cannot use the tool to
            // learn which methods a type happens to declare.
            AssertRefused(result, "exposes no method", nameof(Probe.Unmarked));
            Assert.That(Probe.UnmarkedRan, Is.False, "an unmarked method must be refused before it is invoked");
        }

        [Test]
        public void PointAtTheParameterThatWouldNotConvert()
        {
            // Act
            McpToolResult result = Call(PROBE, nameof(Probe.Add), new JArray { "not a number", 3 });

            // Assert
            AssertRefused(result, "parameter 0", "first", "Int32");
        }

        [Test]
        public void ReportWhatTheInvokedMethodThrewRatherThanTheWrapper()
        {
            // Act
            McpToolResult result = Call(PROBE, nameof(Probe.Boom));

            // Assert — the TargetInvocationException the reflection layer adds says nothing the caller can act on.
            AssertRefused(result, nameof(InvalidOperationException), "probe failure");
            Assert.That(TextOf(result), Does.Not.Contain(nameof(TargetInvocationException)));
        }

        [Test]
        public void RefuseACallThatNamesNoTypeOrNoMethod()
        {
            AssertRefused(Call(string.Empty, nameof(Probe.Boom)), "Provide type and method.");
            AssertRefused(Call(PROBE, string.Empty), "Provide type and method.");
        }

        private McpToolResult Call(string type, string method, JArray? parameters = null, string assembly = "")
        {
            var arguments = new JObject
            {
                ["type"] = type,
                ["method"] = method,
            };

            if (parameters != null)
                arguments["parameters"] = parameters;

            if (assembly.Length > 0)
                arguments["assembly"] = assembly;

            return tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>A refusal has to be an error result that names every part of what was wrong.</summary>
        private static void AssertRefused(McpToolResult result, params string[] mentions)
        {
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);

            string text = TextOf(result);

            foreach (string mention in mentions)
                Assert.That(text, Does.Contain(mention));
        }

        private static string TextOf(McpToolResult result) =>
            result.Payload["content"]![0]!["text"]!.Value<string>()!;

        /// <summary>
        ///     The invocation target: one method per leg of the resolve/bind/invoke path the tool walks. Everything the
        ///     tool is meant to reach carries <see cref="McpCallableAttribute" />, exactly as a real test hook must —
        ///     <see cref="Unmarked" /> is the control that proves the opt-in is what admits the rest.
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedMember.Local")] // every member is reached by name through the tool's reflection
        private static class Probe
        {
            public static bool UnmarkedRan { get; private set; }

            [McpCallable]
            public static int Add(int first, int second) =>
                first + second;

            [McpCallable]
            public static string Describe(string label, bool loud) =>
                loud ? $"{label}!" : label;

            [McpCallable]
            public static T Echo<T>(T value) =>
                value;

            [McpCallable]
            public static void Boom() =>
                throw new InvalidOperationException("probe failure");

            /// <summary>Shaped to resolve on name and arity, so only the missing attribute can refuse it.</summary>
            public static void Unmarked() =>
                UnmarkedRan = true;
        }
    }
}
#endif
