using DCL.UI.SceneDebugConsole.LogHistory;
using NUnit.Framework;

namespace DCL.UI.SceneDebugConsole.Tests
{
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
            logHistory?.ClearLogMessages();
        }

        [Test]
        public void InitializeWithEmptyLists()
        {
            // Assert
            Assert.That(logHistory.FilteredLogMessages, Is.Not.Null);
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddLogMessageToUnfilteredAndFiltered()
        {
            // Arrange
            var logEntry = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message");
            bool eventTriggered = false;
            logHistory.LogMessageAdded += (entry) => eventTriggered = true;

            // Act
            logHistory.AddLogMessage(logEntry);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Message, Does.Contain("Test message"));
            Assert.That(eventTriggered, Is.True);
        }

        [Test]
        public void AddMultipleLogMessages()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message 1");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Test message 2");

            // Act
            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
            Assert.That(logHistory.FilteredLogMessages[0].Type, Is.EqualTo(LogMessageType.Log));
            Assert.That(logHistory.FilteredLogMessages[1].Type, Is.EqualTo(LogMessageType.Error));
        }

        [Test]
        public void ClearAllLogMessages()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Test message 1");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Test message 2");
            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act
            logHistory.ClearLogMessages();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyTextFilter()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Apple pie");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Banana bread");
            var logEntry3 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Cherry cake");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(logEntry3);

            // Act
            var filteredMessages = logHistory.ApplyFilter("Apple", false, false);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(1));
            Assert.That(filteredMessages[0].Message, Does.Contain("Apple"));
        }

        [Test]
        public void ApplyTextFilterCaseInsensitive()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Apple pie");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "BANANA bread");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act
            var filteredMessages = logHistory.ApplyFilter("apple", false, false);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(1));
            Assert.That(filteredMessages[0].Message, Does.Contain("Apple"));
        }

        [Test]
        public void ApplyErrorTypeFilter()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act - Filter out error entries
            var filteredMessages = logHistory.ApplyFilter("", true, false);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(1)); // Should have log, but not error
            Assert.That(filteredMessages.FindAll(e => e.Type == LogMessageType.Error).Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyLogTypeFilter()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act - Filter out log entries
            var filteredMessages = logHistory.ApplyFilter("", false, true);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(1)); // Should have error, but not log
            Assert.That(filteredMessages.FindAll(e => e.Type == LogMessageType.Log).Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyCombinedFilters()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Apple log");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Apple error");
            var logEntry3 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Banana log");
            var logEntry4 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Banana error");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(logEntry3);
            logHistory.AddLogMessage(logEntry4);

            // Act - Filter by text "Apple" and filter out errors
            var filteredMessages = logHistory.ApplyFilter("Apple", true, false);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(1)); // Should only have "Apple log"
            Assert.That(filteredMessages[0].Message, Does.Contain("Apple"));
            Assert.That(filteredMessages[0].Type, Is.EqualTo(LogMessageType.Log));
        }

        [Test]
        public void ApplyNoFilters()
        {
            // Arrange
            var logEntry1 = new SceneDebugConsoleLogEntry(LogMessageType.Log, "Log message");
            var logEntry2 = new SceneDebugConsoleLogEntry(LogMessageType.Error, "Error message");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);

            // Act - No filters applied
            var filteredMessages = logHistory.ApplyFilter("", false, false);

            // Assert
            Assert.That(filteredMessages.Count, Is.EqualTo(2));
        }


    }
}
