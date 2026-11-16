using Arch.System;
using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Tests
{
    public class ReportSinkHandlerShould
    {
        private ICategorySeverityMatrix matrix = null!;
        private RecordingSink sink = null!;
        private ReportSinkHandler handler = null!;
        private GameObject context = null!;

        [SetUp]
        public void SetUp()
        {
            context = new GameObject("report-sink-context");
            matrix = Substitute.For<ICategorySeverityMatrix>();
            matrix.IsEnabled(Arg.Any<string>(), Arg.Any<LogType>()).Returns(true);
            sink = new RecordingSink();
            handler = new ReportSinkHandler(matrix, sink);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ForwardTheCategoryAndSceneWithTheMessage()
        {
            // Arrange
            var scene = new SceneShortInfo(new Vector2Int(3, 4), "my-scene");
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: scene);

            // Act
            handler.Log(LogType.Warning, reportData, context, "a message");

            // Assert
            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(sink.Last.LogType, Is.EqualTo(LogType.Warning));
            Assert.That(sink.Last.Category, Is.EqualTo(ReportCategory.JAVASCRIPT));
            Assert.That(sink.Last.Scene, Is.EqualTo(scene));
            Assert.That(sink.Last.Message, Is.EqualTo("a message"));
            Assert.That(sink.Last.StackTrace, Is.Null);
        }

        [Test]
        public void HonourTheMatrix()
        {
            // Arrange
            matrix.IsEnabled(ReportCategory.REALM, LogType.Log).Returns(false);

            // Act
            handler.Log(LogType.Log, new ReportData(ReportCategory.REALM), context, "filtered");
            handler.Log(LogType.Error, new ReportData(ReportCategory.REALM), context, "kept");

            // Assert
            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(sink.Last.Message, Is.EqualTo("kept"));
        }

        [Test]
        public void FormatTheArguments()
        {
            // Act
            handler.LogFormat(LogType.Log, new ReportData(ReportCategory.MCP), context, "{0} on port {1}", "listening", 8123);

            // Assert
            Assert.That(sink.Last.Message, Is.EqualTo("listening on port 8123"));
        }

        [Test]
        public void IncludeTheExceptionStackTrace()
        {
            // Arrange
            Exception thrown = Throw(new InvalidOperationException("boom"));

            // Act
            handler.LogException(thrown, new ReportData(ReportCategory.UNSPECIFIED), null);

            // Assert
            Assert.That(sink.Last.LogType, Is.EqualTo(LogType.Exception));
            Assert.That(sink.Last.Category, Is.EqualTo(ReportCategory.UNSPECIFIED));
            Assert.That(sink.Last.Message, Is.EqualTo("boom"));
            Assert.That(sink.Last.StackTrace, Is.EqualTo(thrown.StackTrace));
        }

        [Test]
        public void ForwardAnEcsExceptionWithItsOwnReportData()
        {
            // Arrange
            var reportData = new ReportData(ReportCategory.ECS, sceneShortInfo: new SceneShortInfo(new Vector2Int(1, 1), "ecs-scene"));
            var ecsException = new EcsSystemException(Substitute.For<ISystem<float>>(), new ArgumentException("inner"), reportData);

            // Act
            handler.LogException(ecsException);

            // Assert
            Assert.That(sink.Last.LogType, Is.EqualTo(LogType.Exception));
            Assert.That(sink.Last.Category, Is.EqualTo(ReportCategory.ECS));
            Assert.That(sink.Last.Scene.Name, Is.EqualTo("ecs-scene"));
            Assert.That(sink.Last.Message, Is.EqualTo(ecsException.Message));
        }

        private static Exception Throw(Exception exception)
        {
            try { throw exception; }
            catch (Exception e) { return e; }
        }

        private class RecordingSink : IReportSink
        {
            public int Count { get; private set; }
            public ReportLogEntry Last { get; private set; }

            public void Receive(in ReportLogEntry entry)
            {
                Count++;
                Last = entry;
            }
        }
    }
}
