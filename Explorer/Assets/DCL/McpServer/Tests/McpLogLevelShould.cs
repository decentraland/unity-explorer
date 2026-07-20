using DCL.McpServer.Core;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class McpLogLevelShould
    {
        [TestCase("debug", McpLogLevel.Debug)]
        [TestCase("info", McpLogLevel.Info)]
        [TestCase("notice", McpLogLevel.Notice)]
        [TestCase("warning", McpLogLevel.Warning)]
        [TestCase("error", McpLogLevel.Error)]
        [TestCase("critical", McpLogLevel.Critical)]
        [TestCase("alert", McpLogLevel.Alert)]
        [TestCase("emergency", McpLogLevel.Emergency)]
        public void ParseEverySpecLevel(string name, McpLogLevel expected)
        {
            Assert.That(McpLogLevelExtensions.TryParse(name, out McpLogLevel level), Is.True);
            Assert.That(level, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("verbose")]
        [TestCase("Error")] // case-sensitive per spec
        public void RejectUnknownLevels(string? name) =>
            Assert.That(McpLogLevelExtensions.TryParse(name, out _), Is.False);

        [TestCase(McpLogLevel.Debug, "debug")]
        [TestCase(McpLogLevel.Warning, "warning")]
        [TestCase(McpLogLevel.Emergency, "emergency")]
        public void RoundTripThroughTheWireName(McpLogLevel level, string wire)
        {
            Assert.That(level.Wire(), Is.EqualTo(wire));
            Assert.That(McpLogLevelExtensions.TryParse(wire, out McpLogLevel parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(level));
        }

        [Test]
        public void OrderSeveritiesLowToHigh() =>
            Assert.That(McpLogLevel.Error, Is.GreaterThan(McpLogLevel.Warning));
    }
}
