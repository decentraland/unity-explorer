using DCL.McpServer.Core;
using NUnit.Framework;
using System.Reflection;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Covers <c>McpHttpServer.IsAllowed</c>, the DNS-rebinding guard that decides which Origin headers may
    ///     reach the JSON-RPC dispatcher. The method is private static and lives in Core, which these tests do
    ///     not modify, so it is exercised through reflection. If IsAllowed is later exposed as internal
    ///     (with InternalsVisibleTo), replace the reflection shim with a direct call.
    /// </summary>
    public class McpHttpServerShould
    {
        private static readonly MethodInfo IS_ALLOWED =
            typeof(McpHttpServer).GetMethod("IsAllowed", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool IsAllowed(string? origin) =>
            (bool)IS_ALLOWED.Invoke(null, new object?[] { origin })!;

        [TestCase(null)]
        [TestCase("")]
        public void AllowRequestsWithoutAnOrigin(string? origin) =>
            Assert.That(IsAllowed(origin), Is.True);

        [TestCase("http://localhost")]
        [TestCase("http://localhost:8080")]
        [TestCase("http://127.0.0.1")]
        [TestCase("http://127.0.0.1:9001")]
        [TestCase("https://127.0.0.1:9001")]
        public void AllowLoopbackHttpOrigins(string origin) =>
            Assert.That(IsAllowed(origin), Is.True);

        [TestCase("http://evil.com")]
        [TestCase("https://attacker.example:9001")]
        [TestCase("http://127.0.0.1.evil.com")] // rebinding: a foreign host that merely starts with the loopback IP
        public void RejectNonLoopbackHosts(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);

        [TestCase("ftp://127.0.0.1")]
        [TestCase("file:///etc/passwd")]
        public void RejectNonHttpSchemes(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);

        [TestCase("not-a-valid-origin")]
        [TestCase("://missing-scheme")]
        public void RejectMalformedOrigins(string origin) =>
            Assert.That(IsAllowed(origin), Is.False);
    }
}
