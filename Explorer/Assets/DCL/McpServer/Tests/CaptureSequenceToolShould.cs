using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tests
{
    public class CaptureSequenceToolShould
    {
        private static readonly byte[] FAKE_JPG = { 1, 2, 3 };

        private World world = null!;
        private GameObject playerGameObject = null!;
        private ScreenCaptureService capture = null!;
        private CaptureSequenceTool tool = null!;
        private string? writtenDirectory;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerGameObject = new GameObject(nameof(CaptureSequenceToolShould));
            Entity playerEntity = world.Create(new CharacterTransform(playerGameObject.transform));

            capture = new ScreenCaptureService(Substitute.For<ICoroutineRunner>(), world);

            tool = new CaptureSequenceTool(capture, FakeCapture, world, playerEntity);
        }

        [TearDown]
        public void TearDown()
        {
            if (writtenDirectory != null && Directory.Exists(writtenDirectory))
                Directory.Delete(writtenDirectory, recursive: true);

            capture.Dispose();
            world.Dispose();
            Object.DestroyImmediate(playerGameObject);
        }

        [TestCase(99, 9f, CaptureSequenceTool.MAX_COUNT, CaptureSequenceTool.MAX_INTERVAL_SEC)]
        [TestCase(0, 0f, 1, CaptureSequenceTool.MIN_INTERVAL_SEC)]
        [TestCase(3, 0.25f, 3, 0.25f)]
        public void ClampCountAndInterval(int count, float intervalSec, int expectedCount, float expectedInterval)
        {
            // Arrange
            var arguments = new JObject { ["count"] = count, ["intervalSec"] = intervalSec };

            // Act
            (int clampedCount, float clampedInterval) = CaptureSequenceTool.ClampArguments(arguments);

            // Assert
            Assert.That(clampedCount, Is.EqualTo(expectedCount));
            Assert.That(clampedInterval, Is.EqualTo(expectedInterval).Within(0.0001f));
        }

        [Test]
        public void UseTheDocumentedDefaults()
        {
            // Act
            (int count, float intervalSec) = CaptureSequenceTool.ClampArguments(new JObject());

            // Assert
            Assert.That(count, Is.EqualTo(CaptureSequenceTool.DEFAULT_COUNT));
            Assert.That(intervalSec, Is.EqualTo(CaptureSequenceTool.DEFAULT_INTERVAL_SEC));
        }

        [Test]
        public void WriteOneFilePerFrameUnderTheServerDirectory()
        {
            // Act — a single frame needs no interval wait, so the call completes synchronously with the fake capture.
            McpToolResult result = Execute(new JObject { ["count"] = 1 });

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            writtenDirectory = structured["directory"]!.Value<string>();

            Assert.That(writtenDirectory, Does.StartWith(Application.temporaryCachePath));
            Assert.That(structured["count"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(structured["meanIntervalSec"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(structured["late"]!.Value<int>(), Is.EqualTo(0));
            Assert.That(structured["screen"]!["width"]!.Value<int>(), Is.EqualTo(1920));

            var frames = (JArray)structured["frames"]!;
            Assert.That(frames.Count, Is.EqualTo(1));

            string path = frames[0]["path"]!.Value<string>()!;
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(FAKE_JPG));
            Assert.That(frames[0]["intervalFromPreviousSec"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(capture.BusyOwner, Is.Null);
        }

        [Test]
        public void RefuseWhileScreenshotHoldsTheGate()
        {
            // Arrange
            capture.TryAcquire("screenshot");

            // Act
            McpToolResult result = Execute(new JObject { ["count"] = 1 });

            // Assert
            Assert.That(result.Payload["isError"]?.Value<bool>(), Is.True);
            Assert.That(result.Payload["content"]?[0]?["text"]?.Value<string>(), Does.Contain("screenshot"));
            Assert.That(capture.BusyOwner, Is.EqualTo("screenshot"));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Act
            McpToolResult result = Execute(new JObject { ["count"] = 1 });

            // Assert
            var structured = (JObject)result.Payload["structuredContent"]!;
            writtenDirectory = structured["directory"]!.Value<string>();
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        [Test]
        public void DeclareItselfAsWritingFiles()
        {
            // Act
            JObject annotations = tool.Annotations.ToJObject();

            // Assert
            Assert.That(annotations["readOnlyHint"]!.Value<bool>(), Is.False);
            Assert.That(annotations["destructiveHint"]!.Value<bool>(), Is.False);
        }

        private static UniTask<ScreenCaptureService.Frame> FakeCapture(int maxWidth, ScreenCaptureService.Format format, bool worldOnly, CancellationToken ct) =>
            UniTask.FromResult(new ScreenCaptureService.Frame(FAKE_JPG, "image/jpeg", 4, 3, 1920, 1080, 12.5f));

        /// <summary>Only for calls that complete without a real frame wait (refusals and single-frame bursts).</summary>
        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
    }
}
