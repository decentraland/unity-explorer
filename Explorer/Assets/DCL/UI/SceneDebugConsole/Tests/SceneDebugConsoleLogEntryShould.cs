using DCL.UI.SceneDebugConsole.LogHistory;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.Tests
{
    public class SceneDebugConsoleLogEntryShould
    {
        [Test]
        public void CreateLogEntryWithCorrectProperties()
        {
            // Arrange
            var message = "Test log message";
            var stackTrace = "Test stack trace";
            var logType = LogMessageType.Log;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message, stackTrace);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(logType));
            Assert.That(logEntry.Message, Does.Contain(message));
            Assert.That(logEntry.StackTrace, Is.EqualTo(stackTrace));
            Assert.That(logEntry.Timestamp, Is.LessThanOrEqualTo(DateTime.Now));
            Assert.That(logEntry.Color, Is.EqualTo(Color.white));
        }

        [Test]
        public void CreateErrorEntryWithRedColor()
        {
            // Arrange
            var message = "Test error message";
            var logType = LogMessageType.Error;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(logType));
            Assert.That(logEntry.Color, Is.EqualTo(Color.red));
            Assert.That(logEntry.Message, Does.Contain("Error"));
            Assert.That(logEntry.Message, Does.Contain(message));
        }

        [Test]
        public void IncludeTimestampInMessage()
        {
            // Arrange
            var message = "Test message";
            var logType = LogMessageType.Log;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message);

            // Assert
            Assert.That(logEntry.Message, Does.Match(@"\[\d{2}:\d{2}:\d{2}\]")); // Should contain time format [HH:mm:ss]
            Assert.That(logEntry.Message, Does.Contain("[Log]"));
            Assert.That(logEntry.Message, Does.Contain(message));
        }

        [Test]
        public void HandleEmptyMessage()
        {
            // Arrange
            var message = "";
            var logType = LogMessageType.Log;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(logType));
            Assert.That(logEntry.Message, Does.Contain("[Log]"));
            // Should still contain timestamp and type even with empty message
        }

        [Test]
        public void HandleNullStackTrace()
        {
            // Arrange
            var message = "Test message";
            var logType = LogMessageType.Error;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message, null);

            // Assert
            Assert.That(logEntry.StackTrace, Is.Null);
            Assert.That(logEntry.Type, Is.EqualTo(logType));
        }

        [Test]
        public void CreateFromUnityLogType()
        {
            // Arrange
            var message = "Unity log message";
            var stackTrace = "Unity stack trace";

            // Act
            var logEntry = SceneDebugConsoleLogEntry.FromUnityLog(LogType.Log, message, stackTrace);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(LogMessageType.Log));
            Assert.That(logEntry.Message, Does.Contain(message));
            Assert.That(logEntry.StackTrace, Is.EqualTo(stackTrace));
            Assert.That(logEntry.Color, Is.EqualTo(Color.white));
        }

        [Test]
        public void CreateFromUnityErrorType()
        {
            // Arrange
            var message = "Unity error message";
            var stackTrace = "Unity error stack trace";

            // Act
            var logEntry = SceneDebugConsoleLogEntry.FromUnityLog(LogType.Error, message, stackTrace);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(LogMessageType.Error));
            Assert.That(logEntry.Message, Does.Contain(message));
            Assert.That(logEntry.StackTrace, Is.EqualTo(stackTrace));
            Assert.That(logEntry.Color, Is.EqualTo(Color.red));
        }



        [Test]
        public void CreateFromUnityExceptionType()
        {
            // Arrange
            var message = "Unity exception message";
            var stackTrace = "Unity exception stack trace";

            // Act
            var logEntry = SceneDebugConsoleLogEntry.FromUnityLog(LogType.Exception, message, stackTrace);

            // Assert
            Assert.That(logEntry.Type, Is.EqualTo(LogMessageType.Error));
            Assert.That(logEntry.Message, Does.Contain(message));
            Assert.That(logEntry.StackTrace, Is.EqualTo(stackTrace));
            Assert.That(logEntry.Color, Is.EqualTo(Color.red));
        }





        [Test]
        public void DefaultStackTraceIsEmpty()
        {
            // Arrange
            var message = "Test message";
            var logType = LogMessageType.Log;

            // Act
            var logEntry = new SceneDebugConsoleLogEntry(logType, message);

            // Assert
            Assert.That(logEntry.StackTrace, Is.EqualTo(""));
        }
    }
}
