using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Skybox.Systems;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Object = UnityEngine.Object;
using Texture = Decentraland.Common.Texture;
using TextureSlot = DCL.SDKComponents.Skybox.SceneSkyboxComponent.TextureSlot;

namespace DCL.SDKComponents.Skybox.Tests
{
    public class SceneSkyboxHandlerSystemShould : UnitySystemTestBase<SceneSkyboxHandlerSystem>
    {
        private const string SRC_A = "images/a.png";
        private const string SRC_B = "images/b.png";

        // The base fixture's scene world holds SceneShortInfo((0,0), "TEST"); the other scene shares the base parcel like a portable experience
        private static readonly SceneShortInfo OWN_SCENE = new (Vector2Int.zero, "TEST");
        private static readonly SceneShortInfo OTHER_SCENE = new (Vector2Int.zero, "PORTABLE_EXPERIENCE");

        private readonly List<Texture2D> textures = new ();

        private World globalWorld = null!;
        private Entity globalSkyboxEntity;
        private Entity rootEntity;
        private ISceneStateProvider sceneStateProvider = null!;

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

            rootEntity = world.Create(new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY));

            system = new SceneSkyboxHandlerSystem(world, globalWorld, rootEntity, sceneData, PartitionComponent.TOP_PRIORITY, sceneStateProvider);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();

            foreach (Texture2D texture in textures)
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
        public void IgnoreAvatarAndVideoTextures()
        {
            // Arrange
            AddPbSkybox(
                new TextureUnion { AvatarTexture = new AvatarTexture { UserId = "0xdeadbeef" } },
                new TextureUnion { VideoTexture = new VideoTexture { VideoPlayerEntity = 512 } });

            // Act
            system.Update(0);

            // Assert
            Assert.That(Slot(true).LoadingPromise, Is.Null);
            Assert.That(Slot(false).LoadingPromise, Is.Null);
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
            new () { Texture = new Texture { Src = src } };

        private static PBSkybox.Types.Fog Fog(Color color) =>
            new ()
            {
                Color = new ColorGradient
                {
                    Keys = { new ColorKey { Time = 0f, Color = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a } } },
                },
            };

        private static void SetTexture(PBSkybox pbSkybox, bool reflection, TextureUnion? texture)
        {
            if (reflection)
                pbSkybox.ReflectionMap = texture;
            else
                pbSkybox.SkyboxTexture = texture;
        }

        private PBSkybox AddPbSkybox(bool reflection, TextureUnion texture) =>
            reflection ? AddPbSkybox(texture, null) : AddPbSkybox(null, texture);

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

        private SceneSkyboxComponent Component() =>
            world.Get<SceneSkyboxComponent>(rootEntity);

        private TextureSlot Slot(bool reflection)
        {
            SceneSkyboxComponent component = Component();
            return reflection ? component.ReflectionMap : component.SkyboxTexture;
        }

        private ref SceneSkyboxOverrides Overrides() =>
            ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);

        private Texture2D? Override(bool reflection) =>
            reflection ? Overrides().ReflectionMap : Overrides().SkyboxTexture;

        private Texture2D CreateTexture()
        {
            var texture = new Texture2D(2, 2);
            textures.Add(texture);
            return texture;
        }

        private TextureData ResolvePromise(bool reflection)
        {
            var data = new TextureData(CreateTexture());

            // The loading pipeline references the asset once per consumer; the slot releases that reference on clean-up
            data.AcquireRef();

            world.Add(Slot(reflection).LoadingPromise!.Value.Entity, new StreamableLoadingResult<TextureData>(data));
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
            Assert.That(Overrides().Environment, Is.Null);
            Assert.That(Overrides().Owner, Is.Null);
        }
    }
}
