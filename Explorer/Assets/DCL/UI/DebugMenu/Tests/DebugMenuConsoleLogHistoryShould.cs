using DCL.UI.DebugMenu.LogHistory;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace DCL.UI.DebugMenu.Tests
{
    [TestFixture]
    public class DebugMenuConsoleLogHistoryShould
    {
        private DebugMenuConsoleLogHistory logHistory = null!;

        [SetUp]
        public void SetUp()
        {
            logHistory = new DebugMenuConsoleLogHistory();
        }

        [TearDown]
        public void TearDown()
        {
            logHistory = null!;
        }

        [Test]
        public void AddLogMessage_FromWorkerThread_DoesNotBreakMainThreadEnumeration()
        {
            // AddLogMessage is fed by the global Unity log callback, which fires on arbitrary
            // threads while the main thread reads the counters and enumerates FilteredLogMessages
            // (ConsolePanelView.Refresh / ListView binding / copy-all).
            const int ENTRY_COUNT = 50000;

            Exception? mainThreadException = null;
            Exception? workerException = null;

            var worker = new Thread(() =>
            {
                try
                {
                    for (var i = 0; i < ENTRY_COUNT; i++)
                        logHistory.AddLogMessage(new DebugMenuConsoleLogEntry(i % 2 == 0 ? LogMessageType.Log : LogMessageType.Error, "worker entry"));
                }
                catch (Exception e) { workerException = e; }
            });

            worker.Start();

            try
            {
                while (worker.IsAlive)
                {
                    _ = logHistory.LogEntryCount + logHistory.ErrorEntryCount;

                    foreach (DebugMenuConsoleLogEntry entry in logHistory.FilteredLogMessages)
                        _ = entry.Type == LogMessageType.Error;
                }
            }
            catch (Exception e) { mainThreadException = e; }

            worker.Join();

            Assert.That(mainThreadException, Is.Null, $"Main-thread read raced a logging-thread AddLogMessage: {mainThreadException}");
            Assert.That(workerException, Is.Null, $"Logging-thread AddLogMessage threw: {workerException}");
        }

        [Test]
        public void AddLogMessage_FromWorkerThread_IsInvisibleUntilDrained()
        {
            // Arrange
            int eventCallCount = 0;
            logHistory.LogsUpdated += () => eventCallCount++;

            // Act
            var worker = new Thread(() => logHistory.AddLogMessage(new DebugMenuConsoleLogEntry(LogMessageType.Log, "Worker message")));
            worker.Start();
            worker.Join();

            // Assert: nothing surfaces on the ingestion thread
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(0));
            Assert.That(eventCallCount, Is.EqualTo(0));

            // Act: main thread drains
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Message, Does.Contain("Worker message"));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(1));
            Assert.That(eventCallCount, Is.EqualTo(1));
        }

        [Test]
        public void DrainPendingLogs_WithNothingPending_ShouldNotFireEvent()
        {
            // Arrange
            bool eventFired = false;
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(eventFired, Is.False);
        }

        [Test]
        public void AddLogMessage_BeyondPendingCap_ShouldDropOldestPendingEntries()
        {
            // Arrange: 5 entries beyond the pending cap (10000)
            const int PENDING_CAP = 10000;
            const int OVERFLOW = 5;

            for (var i = 0; i < PENDING_CAP + OVERFLOW; i++)
                logHistory.AddLogMessage(new DebugMenuConsoleLogEntry(LogMessageType.Log, $"entry #{i:D5}"));

            // Act
            logHistory.DrainPendingLogs();

            // Assert: oldest OVERFLOW entries were dropped, newest survive in order
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(PENDING_CAP));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(PENDING_CAP));
            Assert.That(logHistory.FilteredLogMessages[0].Message, Does.Contain($"entry #{OVERFLOW:D5}"));
            Assert.That(logHistory.FilteredLogMessages[PENDING_CAP - 1].Message, Does.Contain($"entry #{PENDING_CAP + OVERFLOW - 1:D5}"));
        }

        [Test]
        public void AddLogMessage_WhenNotPaused_ShouldAddToFilteredMessages()
        {
            // Arrange
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message");
            bool eventFired = false;
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(1));
            Assert.That(logHistory.FilteredLogMessages[0].Message, Does.Contain("Test message"));
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void AddLogMessage_WhenPaused_ShouldNotAddToFilteredMessages()
        {
            // Arrange
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message");
            bool eventFired = false;
            logHistory.Paused = true;
            logHistory.LogsUpdated += () => eventFired = true;

            // Act
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(eventFired, Is.False);
        }

        [Test]
        public void AddLogMessage_ShouldUpdateLogEntryCount()
        {
            // Arrange
            var logEntry1 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message 1");
            var logEntry2 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message 2");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Error message");

            // Act
            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(errorEntry);
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(2));
            Assert.That(logHistory.ErrorEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void ClearLogMessages_ShouldClearAllMessages()
        {
            // Arrange
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Error message");
            bool eventFired = false;

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.DrainPendingLogs();
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
            var logEntry1 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "First test message");
            var logEntry2 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Second message");
            var logEntry3 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Third test entry");
            bool eventFired = false;

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(logEntry3);
            logHistory.DrainPendingLogs();
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
            var logEntry1 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test Message");
            var logEntry2 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Another entry");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.DrainPendingLogs();

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
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Error message");
            var warningEntry = new DebugMenuConsoleLogEntry(LogMessageType.Warning, "Warning message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.AddLogMessage(warningEntry);
            logHistory.DrainPendingLogs();

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
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Error message");
            var warningEntry = new DebugMenuConsoleLogEntry(LogMessageType.Warning, "Warning message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.AddLogMessage(warningEntry);
            logHistory.DrainPendingLogs();

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
            var logEntry1 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Important log message");
            var logEntry2 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Another message");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Important error message");

            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.AddLogMessage(errorEntry);
            logHistory.DrainPendingLogs();

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
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message");
            var errorEntry = new DebugMenuConsoleLogEntry(LogMessageType.Error, "Error message");

            logHistory.AddLogMessage(logEntry);
            logHistory.AddLogMessage(errorEntry);
            logHistory.DrainPendingLogs();

            // Act
            logHistory.ApplyFilter("", true, true);

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(2));
        }

        [Test]
        public void LogsUpdated_Event_ShouldFireOncePerDrainWithPendingLogs()
        {
            // Arrange
            var logEntry1 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message 1");
            var logEntry2 = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message 2");
            int eventCallCount = 0;
            logHistory.LogsUpdated += () => eventCallCount++;

            // Act
            logHistory.AddLogMessage(logEntry1);
            logHistory.AddLogMessage(logEntry2);
            logHistory.DrainPendingLogs();

            // Assert: a drained batch fires a single update
            Assert.That(eventCallCount, Is.EqualTo(1));
        }

        [Test]
        public void LogsUpdated_Event_ShouldFireOnClearLogMessages()
        {
            // Arrange
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message");
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

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
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Test message");
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

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
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message");
            logHistory.ApplyFilter("", false, false); // Hide both logs and errors

            // Act
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(1)); // Should still count in totals
        }

        [Test]
        public void AddLogMessage_FilteredOutByText_ShouldNotAddToFilteredButShouldCountInTotals()
        {
            // Arrange
            var logEntry = new DebugMenuConsoleLogEntry(LogMessageType.Log, "Log message");
            logHistory.ApplyFilter("different text", true, true);

            // Act
            logHistory.AddLogMessage(logEntry);
            logHistory.DrainPendingLogs();

            // Assert
            Assert.That(logHistory.FilteredLogMessages.Count, Is.EqualTo(0));
            Assert.That(logHistory.LogEntryCount, Is.EqualTo(1)); // Should still count in totals
        }
    }
}
