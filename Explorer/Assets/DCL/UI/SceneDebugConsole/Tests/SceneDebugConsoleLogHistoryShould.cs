using System;
using System.Linq;
using DCL.UI.SceneDebugConsole.LogHistory;
using NUnit.Framework;

namespace DCL.UI.SceneDebugConsole.Tests
{
    [TestFixture]
    public class SceneDebugConsoleLogHistoryShould
    {
        private SceneDebugConsoleLogHistory logHistory;

        [SetUp]
        public void SetUp()
        {
            logHistory = new SceneDebugConsoleLogHistory();
        }

        [TearDown]
        public void TearDown()
        {
            logHistory = null;
        }

        [Test]
        public void AddLogMessage_WhenNotPaused_ShouldAddToFilteredMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            bool eventFired = false;
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Message, Does.Contain("Test message"));
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void AddLogMessage_WhenPaused_ShouldNotAddToFilteredMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            bool eventFired = false;
            logHistory.Paused = true;
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(eventFired, Is.False);
        }

        [Test]
        public void AddLogMessage_ShouldUpdateLogEntryCount()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message 1");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message 2");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");

            // Act
            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(errorEntry);

            // Assert
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(2));
            Assert.That(logHistory.ErrorEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void ClearLogMessages_ShouldClearAllMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");
            bool eventFired = false;

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.ClearLogMessages();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(0));
            Assert.That(logHistory.ErrorEntryCount, Is.EqualTo(0));
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void ApplyFilter_WithTextFilter_ShouldFilterMessagesByText()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "First test message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Second message");
            var logEntry3 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Third test entry");
            bool eventFired = false;

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(logEntry3);
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.ApplyFilter("test", true, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
            Assert.That(logHistory.FilteredLogMessages.All(msg => msg.Message.Contains("test", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void ApplyFilter_WithCaseInsensitiveTextFilter_ShouldMatchIgnoringCase()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test Message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Another entry");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act
            logHistory.ApplyFilter("TEST", true, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Message.Contains("Test Message"), Is.True);
        }

        [Test]
        public void ApplyFilter_ShowErrorsFalse_ShouldHideErrorMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");
            var warningEntry = new SceneDebugConsoleLogEntry(LogMessageType.Warning, "Warning message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.AddLogMessage(warningEntry);

            // Act
            logHistory.ApplyFilter("", false, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Error), Is.False);
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Log), Is.True);
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Warning), Is.True);
        }

        [Test]
        public void ApplyFilter_ShowLogsFalse_ShouldHideLogMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");
            var warningEntry = new SceneDebugConsoleLogEntry(LogMessageType.Warning, "Warning message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.AddLogMessage(warningEntry);

            // Act
            logHistory.ApplyFilter("", true, false);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Log), Is.False);
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Error), Is.True);
            Assert.That(logHistory.FilteredLogMessages.Any(msg => msg.Type == LogMessageType.Warning), Is.True);
        }

        [Test]
        public void ApplyFilter_WithCombinedTextAndTypeFilters_ShouldApplyBothFilters()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Important log message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Another message");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Important error message");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(errorEntry);

            // Act
            logHistory.ApplyFilter("Important", false, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Type, Is.EqualTo(LogMessageType.Log));
            Assert.That(logHistory.FilteredLogMessages[0].Message.Contains("Important"), Is.True);
        }

        [Test]
        public void ApplyFilter_WithEmptyTextFilter_ShouldOnlyApplyTypeFilters()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);

            // Act
            logHistory.ApplyFilter("", true, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
        }

        [Test]
        public void LogsUpdated_Event_ShouldFireOnAddLogMessage()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            int eventCallCount = 0;
            logHistory.LogsUpdated += () => eventCallCount++;

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(eventCallCount, Is.EqualTo(1));
        }

        [Test]
        public void LogsUpdated_Event_ShouldFireOnClearLogMessages()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            logHistory.AddLogMessage(logEntry);

            int eventCallCount = 0;
            logHistory.LogsUpdated += () => eventCallCount++;

            // Act
            logHistory.ClearLogMessages();

            // Assert
            Assert.That(eventCallCount, Is.EqualTo(1));
        }

        [Test]
        public void LogsUpdated_Event_ShouldFireOnApplyFilter()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            logHistory.AddLogMessage(logEntry);

            int eventCallCount = 0;
            logHistory.LogsUpdated += () => eventCallCount++;

            // Act
            logHistory.ApplyFilter("filter", true, true);

            // Assert
            Assert.That(eventCallCount, Is.EqualTo(1));
        }

        [Test]
        public void AddLogMessage_FilteredOutByType_ShouldNotAddToFilteredButShouldCountInTotals()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            logHistory.ApplyFilter("", false, false); // Hide both logs and errors

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(1)); // Should still count in totals
        }

        [Test]
        public void AddLogMessage_FilteredOutByText_ShouldNotAddToFilteredButShouldCountInTotals()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            logHistory.ApplyFilter("different text", true, true);

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(1)); // Should still count in totals
        }
    }
}
