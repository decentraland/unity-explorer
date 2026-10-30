using Arch.Core;
using CRDT;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.Unity.Transforms.Components;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class GetAudioStateToolShould
    {
        private World sceneWorld = null!;
        private World globalWorld = null!;
        private GameObject playerGo = null!;
        private List<GameObject> emitters = null!;
        private ISceneFacade scene = null!;
        private IScenesCache scenesCache = null!;
        private GetAudioStateTool tool = null!;

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            globalWorld = World.Create();
            emitters = new List<GameObject>();

            playerGo = new GameObject(nameof(GetAudioStateToolShould));
            Entity playerEntity = globalWorld.Create(new CharacterTransform(playerGo.transform));

            scene = Substitute.For<ISceneFacade>();
            scene.EcsExecutor.Returns(new SceneEcsExecutor(sceneWorld));
            scene.SceneStateProvider.IsCurrent.Returns(true);
            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(scene));

            tool = new GetAudioStateTool(scenesCache, globalWorld, playerEntity);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject emitter in emitters)
                Object.DestroyImmediate(emitter);

            Object.DestroyImmediate(playerGo);
            sceneWorld.Dispose();
            globalWorld.Dispose();
        }

        [Test]
        public void ErrorWhenThereIsNoCurrentScene()
        {
            // Arrange
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));

            // Act
            McpToolResult result = Execute(new JObject());

            // Assert
            Assert.That(result.Payload["isError"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void ReportNotCreatedForAnAuthoredSourceWithoutARuntimeComponent()
        {
            // Arrange
            AddAudioSource(600, "sounds/beep.mp3", new Vector3(2f, 0f, 0f), volume: 0.4f, loop: true);

            // Act
            JToken entry = Entries(Execute(new JObject()))[0];

            // Assert
            Assert.That(entry["crdtEntity"]!.Value<int>(), Is.EqualTo(600));
            Assert.That(entry["kind"]!.Value<string>(), Is.EqualTo("audio_source"));
            Assert.That(entry["state"]!.Value<string>(), Is.EqualTo("not_created"));
            Assert.That(entry["playing"]!.Value<bool>(), Is.False);
            Assert.That(entry["source"]!.Value<string>(), Is.EqualTo("sounds/beep.mp3"));
            Assert.That(entry["volume"]!["authored"]!.Value<float>(), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(entry["volume"]!["live"]!.Type, Is.EqualTo(JTokenType.Null));
            Assert.That(entry["loop"]!.Value<bool>(), Is.True);
            Assert.That(entry["distance"]!.Value<float>(), Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void SortEntriesByDistanceToThePlayer()
        {
            // Arrange
            AddAudioSource(601, "far.mp3", new Vector3(5f, 0f, 0f));
            AddAudioSource(602, "near.mp3", new Vector3(1f, 0f, 0f));
            AddAudioSource(603, "mid.mp3", new Vector3(0f, 3f, 0f));
            sceneWorld.Create(new CRDTEntity(604), new PBAudioSource { AudioClipUrl = "nowhere.mp3" });

            // Act
            JArray entries = Entries(Execute(new JObject()));

            // Assert
            Assert.That(entries[0]["crdtEntity"]!.Value<int>(), Is.EqualTo(602));
            Assert.That(entries[1]["crdtEntity"]!.Value<int>(), Is.EqualTo(603));
            Assert.That(entries[2]["crdtEntity"]!.Value<int>(), Is.EqualTo(601));
            Assert.That(entries[3]["crdtEntity"]!.Value<int>(), Is.EqualTo(604), "an emitter without a transform sorts last");
            Assert.That(entries[3]["distance"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void ClassifyStreamsAndVideosByComponent()
        {
            // Arrange
            sceneWorld.Create(new CRDTEntity(610), new PBAudioStream { Url = "https://radio.example/stream", Volume = 0.5f });
            sceneWorld.Create(new CRDTEntity(611), new PBVideoPlayer { Src = "videos/intro.mp4", Loop = true });

            // Act
            JArray entries = Entries(Execute(new JObject()));
            var summary = (JObject)Execute(new JObject()).Payload["structuredContent"]!["summary"]!;

            // Assert
            Assert.That(entries[0]["kind"]!.Value<string>(), Is.EqualTo("audio_stream"));
            Assert.That(entries[0]["liveStream"]!.Value<bool>(), Is.True);
            Assert.That(entries[1]["kind"]!.Value<string>(), Is.EqualTo("video_player"));
            Assert.That(entries[1]["loop"]!.Value<bool>(), Is.True);
            Assert.That(summary["streams"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(summary["videos"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(summary["audioSources"]!.Value<int>(), Is.EqualTo(0));
        }

        [Test]
        public void FilterToPlayingOnly()
        {
            // Arrange
            var entries = new List<AudioEntry>
            {
                new () { CrdtEntity = 1, Kind = AudioKind.AudioSource, Playing = true, Distance = 1f },
                new () { CrdtEntity = 2, Kind = AudioKind.AudioSource, Playing = false, Distance = 2f },
            };

            // Act
            JObject all = AudioStateReader.Build(entries, onlyPlaying: false, limit: 10, sceneIsCurrent: true, note: null);
            JObject playing = AudioStateReader.Build(entries, onlyPlaying: true, limit: 10, sceneIsCurrent: true, note: null);

            // Assert
            Assert.That(((JArray)all["entries"]!).Count, Is.EqualTo(2));
            Assert.That(((JArray)playing["entries"]!).Count, Is.EqualTo(1));
            Assert.That(playing["entries"]![0]!["crdtEntity"]!.Value<int>(), Is.EqualTo(1));
            Assert.That(playing["summary"]!["total"]!.Value<int>(), Is.EqualTo(2), "the summary counts the scene, not the filter");
            Assert.That(playing["summary"]!["playing"]!.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void TruncateAtTheLimit()
        {
            // Arrange
            var entries = new List<AudioEntry>();

            for (var i = 0; i < 5; i++)
                entries.Add(new AudioEntry { CrdtEntity = i, Distance = i });

            // Act
            JObject result = AudioStateReader.Build(entries, onlyPlaying: false, limit: 2, sceneIsCurrent: true, note: null);

            // Assert
            Assert.That(result["returned"]!.Value<int>(), Is.EqualTo(2));
            Assert.That(result["truncated"]!.Value<bool>(), Is.True);
        }

        [Test]
        public void MapMediaStatesToWireNames()
        {
            Assert.That(AudioStateReader.FromMediaState(MediaState.MsPlaying), Is.EqualTo(AudioPlaybackState.Playing));
            Assert.That(AudioStateReader.FromMediaState(MediaState.MsNone), Is.EqualTo(AudioPlaybackState.None));
            Assert.That(AudioStateReader.FromVideoState(VideoState.VsBuffering), Is.EqualTo(AudioPlaybackState.Buffering));
            Assert.That(McpWireEnum<AudioPlaybackState>.ToWire(AudioPlaybackState.Playing), Is.EqualTo("playing"));
            Assert.That(McpWireEnum<AudioPlaybackState>.ToWire(AudioPlaybackState.NotCreated), Is.EqualTo("not_created"));
            Assert.That(McpWireEnum<AudioKind>.ToWire(AudioKind.VideoPlayer), Is.EqualTo("video_player"));
        }

        [Test]
        public void NoteThatAudioIsMutedWhileTheSceneIsNotCurrent()
        {
            // Arrange
            scene.SceneStateProvider.IsCurrent.Returns(false);
            AddAudioSource(620, "beep.mp3", Vector3.zero);

            // Act
            var structured = (JObject)Execute(new JObject()).Payload["structuredContent"]!;

            // Assert
            Assert.That(structured["sceneIsCurrent"]!.Value<bool>(), Is.False);
            Assert.That(structured["note"]!.Value<string>(), Does.Contain("muted"));
        }

        [Test]
        public void KeepTheOutputSchemaInSyncWithTheStructuredPayload()
        {
            // Arrange
            AddAudioSource(630, "beep.mp3", Vector3.one);

            // Act
            var structured = (JObject)Execute(new JObject()).Payload["structuredContent"]!;

            // Assert
            McpSchemaAssert.KeysMatch(tool.OutputSchema, structured);
        }

        private McpToolResult Execute(JObject arguments) =>
            tool.ExecuteAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();

        private static JArray Entries(McpToolResult result) =>
            (JArray)result.Payload["structuredContent"]!["entries"]!;

        private void AddAudioSource(int crdtId, string url, Vector3 position, float volume = 1f, bool loop = false)
        {
            var emitter = new GameObject($"emitter-{crdtId}");
            emitter.transform.position = position;
            emitters.Add(emitter);

            sceneWorld.Create(new CRDTEntity(crdtId), new PBAudioSource { AudioClipUrl = url, Volume = volume, Loop = loop }, new TransformComponent(emitter));
        }
    }
}
