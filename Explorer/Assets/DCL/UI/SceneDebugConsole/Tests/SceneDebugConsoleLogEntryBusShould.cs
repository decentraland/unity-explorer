using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using NUnit.Framework;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.Tests
{
    public class SceneDebugConsoleLogEntryBusShould
    {
        private SceneDebugConsoleLogEntryBus messageBus;

        [SetUp]
        public void SetUp()
        {
            messageBus = new SceneDebugConsoleLogEntryBus();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any event handlers to avoid memory leaks
            messageBus = null;
        }

        [Test]
        public void InitializeWithoutSubscribers()
        {
            // Arrange & Act - Bus is created in SetUp

            // Assert
            Assert.That(messageBus, Is.Not.Null);

            // Should not throw when sending without subscribers
            Assert.DoesNotThrow(() => messageBus.Send("Test message", LogType.Log));
        }

        [Test]
        public void NotifySubscribersWhenMessageSent()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry = default;
            bool eventTriggered = false;

            messageBus.MessageAdded += (entry) =>
            {
                receivedEntry = entry;
                eventTriggered = true;
            };

            // Act
            messageBus.Send("Test message", LogType.Log);

            // Assert
            Assert.That(eventTriggered, Is.True);
            Assert.That(receivedEntry.Type, Is.EqualTo(LogMessageType.Log));
            Assert.That(receivedEntry.Message, Does.Contain("Test message"));
        }

        [Test]
        public void HandleLogTypeMessage()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry = default;
            messageBus.MessageAdded += (entry) => receivedEntry = entry;

            // Act
            messageBus.Send("Log message", LogType.Log);

            // Assert
            Assert.That(receivedEntry.Type, Is.EqualTo(LogMessageType.Log));
            Assert.That(receivedEntry.Message, Does.Contain("Log message"));
            Assert.That(receivedEntry.Color, Is.EqualTo(Color.white));
        }

        [Test]
        public void HandleErrorTypeMessage()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry = default;
            messageBus.MessageAdded += (entry) => receivedEntry = entry;

            // Act
            messageBus.Send("Error message", LogType.Error, "Error stack trace");

            // Assert
            Assert.That(receivedEntry.Type, Is.EqualTo(LogMessageType.Error));
            Assert.That(receivedEntry.Message, Does.Contain("Error message"));
            Assert.That(receivedEntry.StackTrace, Is.EqualTo("Error stack trace"));
            Assert.That(receivedEntry.Color, Is.EqualTo(Color.red));
        }



        [Test]
        public void HandleExceptionTypeMessage()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry = default;
            messageBus.MessageAdded += (entry) => receivedEntry = entry;

            // Act
            messageBus.Send("Exception message", LogType.Exception, "Exception stack trace");

            // Assert
            Assert.That(receivedEntry.Type, Is.EqualTo(LogMessageType.Error));
            Assert.That(receivedEntry.Message, Does.Contain("Exception message"));
            Assert.That(receivedEntry.StackTrace, Is.EqualTo("Exception stack trace"));
            Assert.That(receivedEntry.Color, Is.EqualTo(Color.red));
        }

        [Test]
        public void HandleMultipleSubscribers()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry1 = default;
            SceneDebugConsoleLogEntry receivedEntry2 = default;
            bool event1Triggered = false;
            bool event2Triggered = false;

            messageBus.MessageAdded += (entry) =>
            {
                receivedEntry1 = entry;
                event1Triggered = true;
            };

            messageBus.MessageAdded += (entry) =>
            {
                receivedEntry2 = entry;
                event2Triggered = true;
            };

            // Act
            messageBus.Send("Multi-subscriber message", LogType.Log);

            // Assert
            Assert.That(event1Triggered, Is.True);
            Assert.That(event2Triggered, Is.True);
            Assert.That(receivedEntry1.Message, Does.Contain("Multi-subscriber message"));
            Assert.That(receivedEntry2.Message, Does.Contain("Multi-subscriber message"));
        }

        [Test]
        public void HandleEmptyMessage()
        {
            // Arrange
            SceneDebugConsoleLogEntry receivedEntry = default;
            messageBus.MessageAdded += (entry) => receivedEntry = entry;

            // Act
            messageBus.Send("", LogType.Log);

            // Assert
            Assert.That(receivedEntry.Type, Is.EqualTo(LogMessageType.Log));
            // Message should still be processed even if empty
        }



        [Test]
        public void HandleConsecutiveMessages()
        {
            // Arrange
            int messageCount = 0;
            messageBus.MessageAdded += (entry) => messageCount++;

            // Act
            messageBus.Send("Message 1", LogType.Log);
            messageBus.Send("Message 2", LogType.Error);
            messageBus.Send("Message 3", LogType.Exception);

            // Assert
            Assert.That(messageCount, Is.EqualTo(3));
        }



        [Test]
        public void UnsubscribeFromEvents()
        {
            // Arrange
            int messageCount = 0;
            System.Action<SceneDebugConsoleLogEntry> handler = (entry) => messageCount++;

            messageBus.MessageAdded += handler;
            messageBus.Send("First message", LogType.Log);

            // Act - Unsubscribe
            messageBus.MessageAdded -= handler;
            messageBus.Send("Second message", LogType.Log);

            // Assert
            Assert.That(messageCount, Is.EqualTo(1)); // Only the first message should have been counted
        }


    }
}
