using DCL.Diagnostics;
using DCL.McpServer.Utils;
using DCL.UI.DebugMenu.LogHistory;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class SceneLogBufferShould
    {
        private const int CAPACITY = 1000;

        private SceneLogBuffer buffer = null!;
        private List<SceneLogBuffer.Entry> copied = null!;

        [SetUp]
        public void SetUp()
        {
            buffer = new SceneLogBuffer();
            copied = new List<SceneLogBuffer.Entry>();
        }

        [Test]
        public void TagJavascriptAsSceneAndEverythingElseAsClient()
        {
            // Arrange
            var scene = new SceneShortInfo(new Vector2Int(1, 2), "showcase");

            // Act
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "hello", scene));
            buffer.Receive(Report(LogType.Error, ReportCategory.UNSPECIFIED, "Cannot upsert social settings"));
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: true, limit: 10);

            // Assert
            Assert.That(copied.Count, Is.EqualTo(2));
            Assert.That(copied[0].Source, Is.EqualTo(SceneLogSource.Scene));
            Assert.That(copied[0].Category, Is.EqualTo(ReportCategory.JAVASCRIPT));
            Assert.That(copied[0].SceneName, Is.EqualTo("showcase"));
            Assert.That(copied[0].Message, Does.EndWith("[Log] hello"));
            Assert.That(copied[1].Source, Is.EqualTo(SceneLogSource.Client));
            Assert.That(copied[1].Category, Is.EqualTo(ReportCategory.UNSPECIFIED));
            Assert.That(copied[1].SceneName, Is.Null);
            Assert.That(copied[1].Type, Is.EqualTo(LogMessageType.Error));
        }

        [Test]
        public void HideClientEntriesUnlessAsked()
        {
            // Arrange
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "scene line"));
            buffer.Receive(Report(LogType.Error, ReportCategory.REALM, "client error"));
            buffer.Receive(Report(LogType.Log, ReportCategory.MCP, "server listening"));

            // Act
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: false, limit: 10);

            // Assert
            Assert.That(copied.Count, Is.EqualTo(1));
            Assert.That(copied[0].Message, Does.EndWith("scene line"));
        }

        [Test]
        public void KeepSequenceNumbersAcrossHiddenEntries()
        {
            // Arrange
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "first"));
            buffer.Receive(Report(LogType.Error, ReportCategory.REALM, "hidden"));
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "third"));

            // Act
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: false, limit: 10);

            // Assert
            Assert.That(buffer.LatestSeq, Is.EqualTo(2));
            Assert.That(copied.Count, Is.EqualTo(2));
            Assert.That(copied[0].Seq, Is.EqualTo(0));
            Assert.That(copied[1].Seq, Is.EqualTo(2));
        }

        [Test]
        public void ReturnOnlyEntriesAfterSinceSeq()
        {
            // Arrange
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "a"));
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "b"));
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "c"));

            // Act
            buffer.CopyTo(copied, 0, errorsOnly: false, includeClient: false, limit: 10);

            // Assert
            Assert.That(copied.Count, Is.EqualTo(2));
            Assert.That(copied[0].Seq, Is.EqualTo(1));
            Assert.That(copied[1].Seq, Is.EqualTo(2));
        }

        [Test]
        public void FilterErrorsOnly()
        {
            // Arrange
            buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "log"));
            buffer.Receive(Report(LogType.Error, ReportCategory.JAVASCRIPT, "error"));
            buffer.Receive(Report(LogType.Exception, ReportCategory.JAVASCRIPT, "exception", stackTrace: "at scene.js:1"));

            // Act
            buffer.CopyTo(copied, -1, errorsOnly: true, includeClient: false, limit: 10);

            // Assert
            Assert.That(copied.Count, Is.EqualTo(2));
            Assert.That(copied[0].Message, Does.EndWith("error"));
            Assert.That(copied[1].Message, Does.Contain("exception").And.EndWith("\nat scene.js:1"));
        }

        [Test]
        public void KeepTheNewestEntriesWhenOverTheLimit()
        {
            // Arrange
            for (var i = 0; i < 5; i++)
                buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, i.ToString()));

            // Act
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: false, limit: 2);

            // Assert
            Assert.That(copied.Count, Is.EqualTo(2));
            Assert.That(copied[0].Seq, Is.EqualTo(3));
            Assert.That(copied[1].Seq, Is.EqualTo(4));
        }

        [Test]
        public void WrapAfterCapacity()
        {
            // Arrange
            for (var i = 0; i < CAPACITY + 5; i++)
                buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, i.ToString()));

            // Act
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: false, limit: CAPACITY + 5);

            // Assert
            Assert.That(buffer.LatestSeq, Is.EqualTo(CAPACITY + 4));
            Assert.That(copied.Count, Is.EqualTo(CAPACITY));
            Assert.That(copied[0].Seq, Is.EqualTo(5));
            Assert.That(copied[CAPACITY - 1].Seq, Is.EqualTo(CAPACITY + 4));
        }

        [Test]
        public void AcceptEntriesFromWorkerThreads()
        {
            // Arrange
            const int THREADS = 8;
            const int PER_THREAD = 50;
            var tasks = new Task[THREADS];

            // Act
            for (var t = 0; t < THREADS; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (var i = 0; i < PER_THREAD; i++)
                        buffer.Receive(Report(LogType.Log, ReportCategory.JAVASCRIPT, "parallel"));
                });
            }

            Task.WaitAll(tasks);
            buffer.CopyTo(copied, -1, errorsOnly: false, includeClient: false, limit: THREADS * PER_THREAD);

            // Assert
            Assert.That(buffer.LatestSeq, Is.EqualTo((THREADS * PER_THREAD) - 1));
            Assert.That(copied.Count, Is.EqualTo(THREADS * PER_THREAD));

            for (var i = 0; i < copied.Count; i++)
                Assert.That(copied[i].Seq, Is.EqualTo(i));
        }

        private static ReportLogEntry Report(LogType logType, string category, string message, SceneShortInfo scene = default, string? stackTrace = null) =>
            new (logType, category, scene, message, stackTrace);
    }
}
