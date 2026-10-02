using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.Skybox.Systems;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Entity = Arch.Core.Entity;
using Object = UnityEngine.Object;
using Texture = UnityEngine.Texture;
using TextureSlot = DCL.SDKComponents.Skybox.SceneSkyboxComponent.TextureSlot;

namespace DCL.SDKComponents.Skybox.Tests
{
    public class SceneSkyboxHandlerSystemShould : UnitySystemTestBase<SceneSkyboxHandlerSystem>
    {
        public enum Field
        {
            ReflectionMap,
            SkyboxTexture,
            CloudsTexture,
        }

        private const string SRC_A = "images/a.png";
        private const string SRC_B = "images/b.png";
        private const int VIDEO_PLAYER_A = 512;
        private const int VIDEO_PLAYER_B = 513;

        // The base fixture's scene world holds SceneShortInfo((0,0), "TEST"); the other scene shares the base parcel like a portable experience
        private static readonly SceneShortInfo OWN_SCENE = new (Vector2Int.zero, "TEST");
        private static readonly SceneShortInfo OTHER_SCENE = new (Vector2Int.zero, "PORTABLE_EXPERIENCE");

        private readonly List<Texture> textures = new ();

        private World globalWorld = null!;
        private Entity globalSkyboxEntity;
        private Entity rootEntity;
        private ISceneStateProvider sceneStateProvider = null!;
        private IMediaFactory mediaFactory = null!;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            globalSkyboxEntity = globalWorld.Create(new SceneSkyboxOverrides());

            ISceneData sceneData = Substitute.For<ISceneData>();

            sceneData.TryGetMediaUrl(Arg.Any<string>(), out Arg.Any<URLAddress>())
                     .Returns(c =>
                      {
                          c[1] = URLAddress.FromString(c.ArgAt<string>(0));
                          return true;
                      });

            sceneData.TryGetMediaFileHash(Arg.Any<string>(), out Arg.Any<string>())
                     .Returns(c =>
                      {
                          c[1] = c.ArgAt<string>(0) + "-hash";
                          return true;
                      });

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            mediaFactory = Substitute.For<IMediaFactory>();

            rootEntity = world.Create(new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY));

            system = new SceneSkyboxHandlerSystem(world, globalWorld, rootEntity, sceneData, PartitionComponent.TOP_PRIORITY, sceneStateProvider, mediaFactory);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();

            foreach (Texture texture in textures)
                Object.DestroyImmediate(texture);

            textures.Clear();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CreatePromiseWhenDirty(bool reflection)
        {
            // Arrange
            AddPbSkybox(reflection, FileTexture(SRC_A));

            // Act
            system.Update(0);

            // Assert
            TextureSlot slot = Slot(reflection);
            Assert.That(slot.LoadingPromise, Is.Not.Null);
            Assert.That(slot.LoadingPromise!.Value.LoadingIntention.Src, Is.EqualTo(SRC_A));
            Assert.That(world.IsAlive(slot.LoadingPromise.Value.Entity), Is.True);
            AssertGlobalCleared();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PushOverrideAndOwnerWhenResolvedAndCurrent(bool reflection)
        {
            // Arrange
            AddPbSkybox(reflection, FileTexture(SRC_A));
            system.Update(0);
            TextureData data = ResolvePromise(reflection);

            // Act
            system.Update(0);

            // Assert
            Assert.That(Override(reflection), Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Override(!reflection), Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot(reflection).TextureData, Is.SameAs(data));
            Assert.That(Slot(reflection).LoadingPromise, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NotTouchGlobalWorldWhenNotCurrent(bool reflection)
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            AddPbSkybox(reflection, FileTexture(SRC_A));

            // Act
            system.Update(0);

            // Assert
            Assert.That(Slot(reflection).LoadingPromise, Is.Null);
            AssertGlobalCleared();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReapplyLoadedTextureOnBecomingCurrentWithoutNewPromise(bool reflection)
        {
            // Arrange
            AddPbSkybox(reflection, FileTexture(SRC_A));
            system.Update(0);
            TextureData data = ResolvePromise(reflection);
            system.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertGlobalCleared();

            // Act
            system.OnSceneIsCurrentChanged(true);

            // Assert
            Assert.That(Override(reflection), Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot(reflection).LoadingPromise, Is.Null);
        }

        [Test]
        public void NotClearOnLeaveWhenAnotherSceneOwnsTheOverrides()
        {
            // Arrange
            Texture2D foreignTexture = CreateTexture();
            ref SceneSkyboxOverrides overrides = ref Overrides();
            overrides.SkyboxTexture = foreignTexture;
            overrides.Owner = OTHER_SCENE;

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(foreignTexture));
            Assert.That(Overrides().Owner, Is.EqualTo(OTHER_SCENE));
        }

        [Test]
        public void ClearAndReleaseBothSlotsOnComponentRemoval()
        {
            // Arrange
            AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
            system.Update(0);
            ResolvePromise(true);
            ResolvePromise(false);
            system.Update(0);

            // Act
            world.Remove<PBSkybox>(rootEntity);
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Slot(true).TextureData, Is.Null);
            Assert.That(Slot(false).TextureData, Is.Null);
        }

        [Test]
        public void ForgetUnresolvedPromisesAndClearOnFinalize()
        {
            // Arrange
            AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
            system.Update(0);
            ResolvePromise(false);
            system.Update(0);
            Entity pendingPromise = Slot(true).LoadingPromise!.Value.Entity;

            // Act
            system.FinalizeComponents(world.Query(new QueryDescription().WithAll<CRDTEntity>()));

            // Assert
            AssertGlobalCleared();
            Assert.That(world.IsAlive(pendingPromise), Is.False);
            Assert.That(Slot(true).LoadingPromise, Is.Null);
            Assert.That(Slot(false).TextureData, Is.Null);
        }

        [Test]
        public void IgnoreAvatarTextures()
        {
            // Arrange
            AddPbSkybox(
                new TextureUnion { AvatarTexture = new AvatarTexture { UserId = "0xdeadbeef" } },
                new TextureUnion { AvatarTexture = new AvatarTexture { UserId = "0xdeadbeef" } });

            // Act
            system.Update(0);

            // Assert
            Assert.That(Slot(true).LoadingPromise, Is.Null);
            Assert.That(Slot(false).LoadingPromise, Is.Null);
            Assert.That(Slot(true).IsVideoTexture, Is.False);
            mediaFactory.DidNotReceive().TryAddScreenSpaceConsumer(Arg.Any<CRDTEntity>(), out Arg.Any<TextureData?>());
            AssertGlobalCleared();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeepPromiseWhenIntentionIsUnchanged(bool reflection)
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(reflection, FileTexture(SRC_A));
            system.Update(0);
            Entity promiseEntity = Slot(reflection).LoadingPromise!.Value.Entity;

            // Act
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Slot(reflection).LoadingPromise!.Value.Entity, Is.EqualTo(promiseEntity));
            Assert.That(world.IsAlive(promiseEntity), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CreateNewPromiseWhenSrcChanges(bool reflection)
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(reflection, FileTexture(SRC_A));
            system.Update(0);
            Entity oldPromiseEntity = Slot(reflection).LoadingPromise!.Value.Entity;

            // Act
            SetTexture(pbSkybox, reflection, FileTexture(SRC_B));
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            TextureSlot slot = Slot(reflection);
            Assert.That(world.IsAlive(oldPromiseEntity), Is.False);
            Assert.That(slot.LoadingPromise!.Value.Entity, Is.Not.EqualTo(oldPromiseEntity));
            Assert.That(slot.LoadingPromise.Value.LoadingIntention.Src, Is.EqualTo(SRC_B));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeepDefaultsWhenLoadingFails(bool reflection)
        {
            // Arrange
            AddPbSkybox(reflection, FileTexture(SRC_A));
            system.Update(0);
            FailPromise(reflection);

            // Act
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Slot(reflection).LoadingPromise, Is.Null);
            Assert.That(Slot(reflection).TextureData, Is.Null);
        }

        [Test]
        public void ReleaseOnlyTheClearedSlot()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
            system.Update(0);
            ResolvePromise(true);
            TextureData skyboxData = ResolvePromise(false);
            system.Update(0);

            // Act
            pbSkybox.ReflectionMap = null;
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Overrides().ReflectionMap, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(skyboxData.EnsureTexture2D()));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot(true).TextureData, Is.Null);
            Assert.That(Slot(false).TextureData, Is.SameAs(skyboxData));
        }

        [Test]
        public void PushBothWhenBothFieldsAreSet()
        {
            // Arrange
            AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
            system.Update(0);
            TextureData reflectionData = ResolvePromise(true);
            TextureData skyboxData = ResolvePromise(false);

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().ReflectionMap, Is.SameAs(reflectionData.EnsureTexture2D()));
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(skyboxData.EnsureTexture2D()));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void LoadAndPushCloudsTexture()
        {
            // Arrange
            AddPbSkybox(clouds: new PBSkybox.Types.Clouds { Texture = FileTexture(SRC_A) });
            system.Update(0);
            Assert.That(Slot(Field.CloudsTexture).LoadingPromise!.Value.LoadingIntention.Src, Is.EqualTo(SRC_A));
            TextureData data = ResolvePromise(Field.CloudsTexture);

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().CloudsTexture, Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Overrides().ReflectionMap, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot(Field.CloudsTexture).TextureData, Is.SameAs(data));
        }

        [Test]
        public void ReleaseCloudsTextureWhenCloudsGroupIsUnset()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(clouds: new PBSkybox.Types.Clouds { Texture = FileTexture(SRC_A) });
            system.Update(0);
            TextureData data = ResolvePromise(Field.CloudsTexture);
            system.Update(0);

            // Act
            pbSkybox.Clouds = null;
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Overrides().CloudsTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot(Field.CloudsTexture).TextureData, Is.Null);
            Assert.That(data.CanBeDisposed(), Is.True);
        }

        [TestCase(Field.ReflectionMap)]
        [TestCase(Field.SkyboxTexture)]
        [TestCase(Field.CloudsTexture)]
        public void ResolveVideoTextureThroughMediaFactory(Field field)
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            AddPbSkybox(field, VideoTexture(VIDEO_PLAYER_A));

            // Act
            system.Update(0);

            // Assert
            mediaFactory.Received(1).TryAddScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A), out Arg.Any<TextureData?>());
            TextureSlot slot = Slot(field);
            Assert.That(slot.IsVideoTexture, Is.True);
            Assert.That(slot.VideoPlayerEntity, Is.EqualTo(new CRDTEntity(VIDEO_PLAYER_A)));
            Assert.That(slot.LoadingPromise, Is.Null);
            Assert.That(slot.TextureData, Is.SameAs(data));
            Assert.That(Override(field), Is.SameAs(data.Asset.Texture));
            Assert.That(Override(field), Is.InstanceOf<RenderTexture>());
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void RetryVideoConsumerUntilVideoPlayerIsReady()
        {
            // Arrange
            AddPbSkybox(Field.SkyboxTexture, VideoTexture(VIDEO_PLAYER_A));

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            mediaFactory.Received(2).TryAddScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A), out Arg.Any<TextureData?>());
            Assert.That(Slot(Field.SkyboxTexture).IsVideoTexture, Is.True);
            Assert.That(Slot(Field.SkyboxTexture).TextureData, Is.Null);
            AssertGlobalCleared();

            // Act
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            system.Update(0);

            // Assert
            Assert.That(Slot(Field.SkyboxTexture).TextureData, Is.SameAs(data));
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.Asset.Texture));
        }

        [Test]
        public void DropVideoTextureWhenVideoPlayerIsDeletedAndRetry()
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            AddPbSkybox(Field.SkyboxTexture, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.Asset.Texture));

            // Act: the video player entity is gone
            mediaFactory.HasVideoTexture(new CRDTEntity(VIDEO_PLAYER_A)).Returns(false);
            mediaFactory.TryAddScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A), out Arg.Any<TextureData?>()).Returns(false);
            system.Update(0);

            // Assert: texture released, the slot keeps waiting for the same video player
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Slot(Field.SkyboxTexture).IsVideoTexture, Is.True);
            Assert.That(Slot(Field.SkyboxTexture).TextureData, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);

            // Act: the video player entity comes back
            TextureData recreated = SetUpVideoPlayer(VIDEO_PLAYER_A);
            system.Update(0);

            // Assert
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(recreated.Asset.Texture));
        }

        [Test]
        public void NotReAddVideoConsumerForTheSameVideoPlayer()
        {
            // Arrange
            SetUpVideoPlayer(VIDEO_PLAYER_A);
            PBSkybox pbSkybox = AddPbSkybox(Field.SkyboxTexture, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            pbSkybox.SkyboxTexture = VideoTexture(VIDEO_PLAYER_A);
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            mediaFactory.Received(1).TryAddScreenSpaceConsumer(Arg.Any<CRDTEntity>(), out Arg.Any<TextureData?>());
            mediaFactory.DidNotReceive().RemoveScreenSpaceConsumer(Arg.Any<CRDTEntity>());
        }

        [Test]
        public void RemoveVideoConsumerWhenSourceChangesToFile()
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            PBSkybox pbSkybox = AddPbSkybox(Field.SkyboxTexture, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            pbSkybox.SkyboxTexture = FileTexture(SRC_A);
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            Assert.That(data.CanBeDisposed(), Is.True);
            TextureSlot slot = Slot(Field.SkyboxTexture);
            Assert.That(slot.IsVideoTexture, Is.False);
            Assert.That(slot.TextureData, Is.Null);
            Assert.That(slot.LoadingPromise!.Value.LoadingIntention.Src, Is.EqualTo(SRC_A));
            Assert.That(Overrides().SkyboxTexture, Is.Null);
        }

        [Test]
        public void SwitchVideoConsumerWhenVideoPlayerChanges()
        {
            // Arrange
            TextureData dataA = SetUpVideoPlayer(VIDEO_PLAYER_A);
            TextureData dataB = SetUpVideoPlayer(VIDEO_PLAYER_B);
            PBSkybox pbSkybox = AddPbSkybox(Field.ReflectionMap, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            pbSkybox.ReflectionMap = VideoTexture(VIDEO_PLAYER_B);
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            mediaFactory.Received(1).TryAddScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_B), out Arg.Any<TextureData?>());
            Assert.That(dataA.CanBeDisposed(), Is.True);
            Assert.That(Slot(Field.ReflectionMap).TextureData, Is.SameAs(dataB));
            Assert.That(Overrides().ReflectionMap, Is.SameAs(dataB.Asset.Texture));
        }

        [Test]
        public void RemoveVideoConsumerOnComponentRemoval()
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            AddPbSkybox(Field.CloudsTexture, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            world.Remove<PBSkybox>(rootEntity);
            system.Update(0);

            // Assert
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Slot(Field.CloudsTexture).IsVideoTexture, Is.False);
            AssertGlobalCleared();
        }

        [Test]
        public void RemoveVideoConsumerOnLeaveAndReAddOnReturn()
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            AddPbSkybox(Field.SkyboxTexture, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            Assert.That(Slot(Field.SkyboxTexture).IsVideoTexture, Is.False);
            Assert.That(Slot(Field.SkyboxTexture).TextureData, Is.Null);
            AssertGlobalCleared();

            // Act
            system.OnSceneIsCurrentChanged(true);

            // Assert
            mediaFactory.Received(2).TryAddScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A), out Arg.Any<TextureData?>());
            Assert.That(Slot(Field.SkyboxTexture).TextureData, Is.SameAs(data));
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.Asset.Texture));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void RemoveVideoConsumerOnFinalize()
        {
            // Arrange
            TextureData data = SetUpVideoPlayer(VIDEO_PLAYER_A);
            AddPbSkybox(Field.ReflectionMap, VideoTexture(VIDEO_PLAYER_A));
            system.Update(0);

            // Act
            system.FinalizeComponents(world.Query(new QueryDescription().WithAll<CRDTEntity>()));

            // Assert
            mediaFactory.Received(1).RemoveScreenSpaceConsumer(new CRDTEntity(VIDEO_PLAYER_A));
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Slot(Field.ReflectionMap).TextureData, Is.Null);
            AssertGlobalCleared();
        }

        [Test]
        public void PushEnvironmentWithOwnerWhenDirty()
        {
            // Arrange
            AddPbSkybox(Fog(Color.red));

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.SameAs(Component().Environment));
            Assert.That(environment!.FogColor, Is.Not.Null);
            Assert.That(environment.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.red));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PushFogDensityWhenDirty()
        {
            // Arrange
            AddPbSkybox(Fog(0.02f));

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.FogDensity, Is.EqualTo(0.02f));
            Assert.That(environment.FogColor, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PushSunVisibilityWhenDirty()
        {
            // Arrange
            world.Add(rootEntity, new PBSkybox { IsDirty = true, Sun = new PBSkybox.Types.Sun { Visible = false } });

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.SunVisible, Is.False);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PushRimColorWhenDirty()
        {
            // Arrange
            world.Add(rootEntity, new PBSkybox
            {
                IsDirty = true,
                SkyColors = new PBSkybox.Types.SkyColors { Rim = Fog(Color.blue).Color },
            });

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.Rim, Is.Not.Null);
            Assert.That(environment.Rim!.Evaluate(0.5f), Is.EqualTo(Color.blue));
            Assert.That(environment.Horizon, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void KeepSameProfileReferenceWhenNotDirty()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(Fog(Color.red));
            system.Update(0);
            SceneEnvironmentProfile? environment = Overrides().Environment;

            // Act
            pbSkybox.IsDirty = false;
            system.Update(0);

            // Assert
            Assert.That(environment, Is.Not.Null);
            Assert.That(Overrides().Environment, Is.SameAs(environment));
            Assert.That(Component().Environment, Is.SameAs(environment));
        }

        [Test]
        public void ReplaceProfileWhenDirty()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(Fog(Color.red));
            system.Update(0);
            SceneEnvironmentProfile? previous = Overrides().Environment;

            // Act
            pbSkybox.Fog = Fog(Color.blue);
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.Not.SameAs(previous));
            Assert.That(environment!.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.blue));
        }

        [Test]
        public void ClearEnvironmentOnComponentRemoval()
        {
            // Arrange
            AddPbSkybox(Fog(Color.red));
            system.Update(0);

            // Act
            world.Remove<PBSkybox>(rootEntity);
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Component().Environment, Is.Null);
        }

        [Test]
        public void ClearEnvironmentOnLeaveAndRebuildFromProtoOnCurrent()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(Fog(Color.red));
            system.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertGlobalCleared();

            // Arrange: the scene changes the fog while the player is away; the dirty flag is reset before it becomes current again
            pbSkybox.Fog = Fog(Color.blue);
            pbSkybox.IsDirty = false;

            // Act
            system.OnSceneIsCurrentChanged(true);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.blue));
            Assert.That(Component().Environment, Is.SameAs(environment));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void ClearEnvironmentOnFinalize()
        {
            // Arrange
            AddPbSkybox(Fog(Color.red));
            system.Update(0);

            // Act
            system.FinalizeComponents(world.Query(new QueryDescription().WithAll<CRDTEntity>()));

            // Assert
            AssertGlobalCleared();
            Assert.That(Component().Environment, Is.Null);
        }

        [Test]
        public void NotClearForeignEnvironmentOnLeave()
        {
            // Arrange
            SceneEnvironmentProfile? foreignEnvironment = SceneEnvironmentProfile.FromProto(new PBSkybox { Fog = Fog(Color.red) });
            ref SceneSkyboxOverrides overrides = ref Overrides();
            overrides.Environment = foreignEnvironment;
            overrides.Owner = OTHER_SCENE;

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            Assert.That(foreignEnvironment, Is.Not.Null);
            Assert.That(Overrides().Environment, Is.SameAs(foreignEnvironment));
            Assert.That(Overrides().Owner, Is.EqualTo(OTHER_SCENE));
        }

        [Test]
        public void NotPushEnvironmentWhenNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            AddPbSkybox(Fog(Color.red));

            // Act
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Component().Environment, Is.Null);
        }

        [Test]
        public void PushNullEnvironmentWhenAllGroupsUnset()
        {
            // Arrange
            AddPbSkybox(false, FileTexture(SRC_A));
            system.Update(0);
            ResolvePromise(false);

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Overrides().Environment, Is.Null);
            Assert.That(Component().Environment, Is.Null);
        }

        private static TextureUnion FileTexture(string src) =>
            new () { Texture = new Decentraland.Common.Texture { Src = src } };

        private static TextureUnion VideoTexture(int videoPlayerEntity) =>
            new () { VideoTexture = new VideoTexture { VideoPlayerEntity = (uint)videoPlayerEntity } };

        private static PBSkybox.Types.Fog Fog(float density) =>
            new () { Density = density };

        private static PBSkybox.Types.Fog Fog(Color color) =>
            new ()
            {
                Color = new ColorGradient
                {
                    Keys = { new ColorKey { Time = 0f, Color = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a } } },
                },
            };

        private static Field ToField(bool reflection) =>
            reflection ? Field.ReflectionMap : Field.SkyboxTexture;

        private static void SetTexture(PBSkybox pbSkybox, bool reflection, TextureUnion? texture) =>
            SetTexture(pbSkybox, ToField(reflection), texture);

        private static void SetTexture(PBSkybox pbSkybox, Field field, TextureUnion? texture)
        {
            switch (field)
            {
                case Field.ReflectionMap:
                    pbSkybox.ReflectionMap = texture;
                    break;
                case Field.SkyboxTexture:
                    pbSkybox.SkyboxTexture = texture;
                    break;
                case Field.CloudsTexture:
                    pbSkybox.Clouds = texture == null ? null : new PBSkybox.Types.Clouds { Texture = texture };
                    break;
            }
        }

        private PBSkybox AddPbSkybox(bool reflection, TextureUnion texture) =>
            AddPbSkybox(ToField(reflection), texture);

        private PBSkybox AddPbSkybox(Field field, TextureUnion texture)
        {
            var pbSkybox = new PBSkybox { IsDirty = true };
            SetTexture(pbSkybox, field, texture);
            world.Add(rootEntity, pbSkybox);
            return pbSkybox;
        }

        private PBSkybox AddPbSkybox(TextureUnion? reflectionMap, TextureUnion? skyboxTexture)
        {
            var pbSkybox = new PBSkybox { IsDirty = true };
            SetTexture(pbSkybox, true, reflectionMap);
            SetTexture(pbSkybox, false, skyboxTexture);
            world.Add(rootEntity, pbSkybox);
            return pbSkybox;
        }

        private PBSkybox AddPbSkybox(PBSkybox.Types.Fog fog)
        {
            var pbSkybox = new PBSkybox { IsDirty = true, Fog = fog };
            world.Add(rootEntity, pbSkybox);
            return pbSkybox;
        }

        private PBSkybox AddPbSkybox(PBSkybox.Types.Clouds clouds)
        {
            var pbSkybox = new PBSkybox { IsDirty = true, Clouds = clouds };
            world.Add(rootEntity, pbSkybox);
            return pbSkybox;
        }

        private SceneSkyboxComponent Component() =>
            world.Get<SceneSkyboxComponent>(rootEntity);

        private TextureSlot Slot(bool reflection) =>
            Slot(ToField(reflection));

        private TextureSlot Slot(Field field)
        {
            SceneSkyboxComponent component = Component();

            return field switch
                   {
                       Field.ReflectionMap => component.ReflectionMap,
                       Field.SkyboxTexture => component.SkyboxTexture,
                       _ => component.CloudsTexture,
                   };
        }

        private ref SceneSkyboxOverrides Overrides() =>
            ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);

        private Texture? Override(bool reflection) =>
            Override(ToField(reflection));

        private Texture? Override(Field field) =>
            field switch
            {
                Field.ReflectionMap => Overrides().ReflectionMap,
                Field.SkyboxTexture => Overrides().SkyboxTexture,
                _ => Overrides().CloudsTexture,
            };

        private Texture2D CreateTexture()
        {
            var texture = new Texture2D(2, 2);
            textures.Add(texture);
            return texture;
        }

        /// <summary>
        ///     Builds the texture data a video player entity would hold: a pooled render texture wrapped as a video texture.
        /// </summary>
        private TextureData CreateVideoTextureData()
        {
            var renderTexture = new RenderTexture(2, 2, 0);
            textures.Add(renderTexture);

            IObjectPool<RenderTexture> pool = Substitute.For<IObjectPool<RenderTexture>>();
            pool.Get().Returns(renderTexture);

            return new TextureData(AnyTexture.FromVideoTextureData(new VideoTextureData(new VideoTextureConsumer(pool), default(MediaPlayerComponent))));
        }

        /// <summary>
        ///     Makes the media factory resolve the video player, referencing the texture data on every consumer it adds.
        /// </summary>
        private TextureData SetUpVideoPlayer(int videoPlayerEntity)
        {
            TextureData data = CreateVideoTextureData();

            mediaFactory.HasVideoTexture(new CRDTEntity(videoPlayerEntity)).Returns(true);

            mediaFactory.TryAddScreenSpaceConsumer(new CRDTEntity(videoPlayerEntity), out Arg.Any<TextureData?>())
                        .Returns(c =>
                         {
                             data.AcquireRef();
                             c[1] = data;
                             return true;
                         });

            return data;
        }

        private TextureData ResolvePromise(bool reflection) =>
            ResolvePromise(ToField(reflection));

        private TextureData ResolvePromise(Field field)
        {
            var data = new TextureData(CreateTexture());

            // The loading pipeline references the asset once per consumer; the slot releases that reference on clean-up
            data.AcquireRef();

            world.Add(Slot(field).LoadingPromise!.Value.Entity, new StreamableLoadingResult<TextureData>(data));
            return data;
        }

        private void FailPromise(bool reflection)
        {
            world.Add(Slot(reflection).LoadingPromise!.Value.Entity,
                new StreamableLoadingResult<TextureData>(ReportData.UNSPECIFIED, new StreamableLoadingException(LogType.Warning, "simulated failure")));
        }

        private void AssertGlobalCleared()
        {
            Assert.That(Overrides().ReflectionMap, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().CloudsTexture, Is.Null);
            Assert.That(Overrides().Environment, Is.Null);
            Assert.That(Overrides().Owner, Is.Null);
        }
    }
}
