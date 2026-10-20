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
using Texture = UnityEngine.Texture;
using TextureWrapMode = UnityEngine.TextureWrapMode;
using TextureSlot = DCL.SDKComponents.Skybox.SceneSkyboxComponent.TextureSlot;
using Vector3 = UnityEngine.Vector3;

namespace DCL.SDKComponents.Skybox.Tests
{
    public class SceneSkyboxHandlerSystemShould : UnitySystemTestBase<SceneSkyboxHandlerSystem>
    {
        private const string SRC_A = "images/a.png";
        private const string SRC_B = "images/b.png";

        // The base fixture's scene world holds SceneShortInfo((0,0), "TEST"); the other scene shares the base parcel like a portable experience
        private static readonly SceneShortInfo OWN_SCENE = new (Vector2Int.zero, "TEST");
        private static readonly SceneShortInfo OTHER_SCENE = new (Vector2Int.zero, "PORTABLE_EXPERIENCE");

        private readonly List<Texture> textures = new ();

        private World globalWorld = null!;
        private Entity globalSkyboxEntity;
        private Entity rootEntity;
        private ISceneData sceneData = null!;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            globalSkyboxEntity = globalWorld.Create(new SceneSkyboxOverrides());

            sceneData = Substitute.For<ISceneData>();

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

            foreach (Texture texture in textures)
                Object.DestroyImmediate(texture);

            textures.Clear();
        }

        [Test]
        public void CreatePromiseWhenDirty()
        {
            // Arrange
            AddPbSkybox(SRC_A);

            // Act
            system.Update(0);

            // Assert
            TextureSlot slot = Slot();
            Assert.That(slot.LoadingPromise, Is.Not.Null);
            Assert.That(slot.LoadingPromise!.Value.LoadingIntention.Src, Is.EqualTo(SRC_A));
            Assert.That(world.IsAlive(slot.LoadingPromise.Value.Entity), Is.True);
            AssertGlobalCleared();
        }

        [Test]
        public void PushOverrideAndOwnerWhenResolvedAndCurrent()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Overrides().ReflectionSource, Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot().TextureData, Is.SameAs(data));
            Assert.That(Slot().LoadingPromise, Is.Null);
        }

        [Test]
        public void NotTouchGlobalWorldWhenNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            AddPbSkybox(SRC_A);

            // Act
            system.Update(0);

            // Assert
            Assert.That(Slot().LoadingPromise, Is.Null);
            AssertGlobalCleared();
        }

        [Test]
        public void ReapplyLoadedTextureOnBecomingCurrentWithoutNewPromise()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertGlobalCleared();

            // Act
            system.OnSceneIsCurrentChanged(true);

            // Assert
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.EnsureTexture2D()));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Slot().LoadingPromise, Is.Null);
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
        public void ClearAndReleaseSlotOnComponentRemoval()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);

            // Act
            world.Remove<PBSkybox>(rootEntity);
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Slot().TextureData, Is.Null);
            Assert.That(data.CanBeDisposed(), Is.True);
        }

        [Test]
        public void ForgetUnresolvedPromiseAndClearOnFinalize()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            world.Add(rootEntity, new PBSun { IsDirty = true, Intensity = 2f });
            system.Update(0);
            Entity pendingPromise = Slot().LoadingPromise!.Value.Entity;
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));

            // Act
            system.FinalizeComponents(world.Query(new QueryDescription().WithAll<CRDTEntity>()));

            // Assert
            AssertGlobalCleared();
            Assert.That(world.IsAlive(pendingPromise), Is.False);
            Assert.That(Slot().LoadingPromise, Is.Null);
            Assert.That(Component().Environment, Is.Null);
        }

        [Test]
        public void RequestTheSkyTextureWithRepeatWrapAndBilinearFilter()
        {
            // Arrange
            AddPbSkybox(SRC_A);

            // Act
            system.Update(0);

            // Assert
            GetTextureIntention intention = Slot().LoadingPromise!.Value.LoadingIntention;
            Assert.That(intention.Src, Is.EqualTo(SRC_A));
            Assert.That(intention.FileHash, Is.EqualTo(SRC_A + "-hash"));
            Assert.That(intention.WrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(intention.FilterMode, Is.EqualTo(FilterMode.Bilinear));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReleaseTheSlotWhenTheSkyboxTextureIsUnsetOrEmpty(bool unset)
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);

            // Act
            if (unset)
                pbSkybox.ClearSkyboxTexture();
            else
                pbSkybox.SkyboxTexture = string.Empty;

            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Slot().TextureData, Is.Null);
            Assert.That(Slot().LoadingPromise, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void ReleaseAndKeepDefaultsWhenTheSrcCannotBeResolved()
        {
            // Arrange
            sceneData.TryGetMediaUrl(SRC_B, out Arg.Any<URLAddress>())
                     .Returns(c =>
                      {
                          c[1] = URLAddress.EMPTY;
                          return false;
                      });

            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);

            // Act
            pbSkybox.SkyboxTexture = SRC_B;
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Slot().TextureData, Is.Null);
            Assert.That(Slot().LoadingPromise, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void NotReloadALoadedTextureForTheSameSrc()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);

            // Act
            pbSkybox.SkyboxTexture = SRC_A;
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Slot().LoadingPromise, Is.Null);
            Assert.That(Slot().TextureData, Is.SameAs(data));
            Assert.That(data.CanBeDisposed(), Is.False);
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.EnsureTexture2D()));
        }

        [Test]
        public void KeepPromiseWhenIntentionIsUnchanged()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            system.Update(0);
            Entity promiseEntity = Slot().LoadingPromise!.Value.Entity;

            // Act
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Slot().LoadingPromise!.Value.Entity, Is.EqualTo(promiseEntity));
            Assert.That(world.IsAlive(promiseEntity), Is.True);
        }

        [Test]
        public void CreateNewPromiseWhenSrcChanges()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            system.Update(0);
            Entity oldPromiseEntity = Slot().LoadingPromise!.Value.Entity;

            // Act
            pbSkybox.SkyboxTexture = SRC_B;
            pbSkybox.IsDirty = true;
            system.Update(0);

            // Assert
            TextureSlot slot = Slot();
            Assert.That(world.IsAlive(oldPromiseEntity), Is.False);
            Assert.That(slot.LoadingPromise!.Value.Entity, Is.Not.EqualTo(oldPromiseEntity));
            Assert.That(slot.LoadingPromise.Value.LoadingIntention.Src, Is.EqualTo(SRC_B));
        }

        [Test]
        public void KeepDefaultsWhenLoadingFails()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            system.Update(0);
            FailPromise();

            // Act
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Slot().LoadingPromise, Is.Null);
            Assert.That(Slot().TextureData, Is.Null);
        }

        // Next iteration: the explicit reflection map (PBSkybox.reflection_map) was removed from the protocol, the handler
        // and SceneSkyboxOverrides; reflections are always derived from the skybox texture. Restore with that field.
        // [Test]
        // public void ReleaseOnlyTheClearedSlot()
        // {
        //     // Arrange
        //     PBSkybox pbSkybox = AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
        //     system.Update(0);
        //     ResolvePromise(true);
        //     TextureData skyboxData = ResolvePromise(false);
        //     system.Update(0);
        //
        //     // Act
        //     pbSkybox.ReflectionMap = null;
        //     pbSkybox.IsDirty = true;
        //     system.Update(0);
        //
        //     // Assert
        //     Assert.That(Overrides().ReflectionMap, Is.Null);
        //     Assert.That(Overrides().SkyboxTexture, Is.SameAs(skyboxData.EnsureTexture2D()));
        //     Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        //     Assert.That(Slot(true).TextureData, Is.Null);
        //     Assert.That(Slot(false).TextureData, Is.SameAs(skyboxData));
        // }
        //
        // [Test]
        // public void PushBothWhenBothFieldsAreSet()
        // {
        //     // Arrange
        //     AddPbSkybox(FileTexture(SRC_A), FileTexture(SRC_B));
        //     system.Update(0);
        //     TextureData reflectionData = ResolvePromise(true);
        //     TextureData skyboxData = ResolvePromise(false);
        //
        //     // Act
        //     system.Update(0);
        //
        //     // Assert
        //     Assert.That(Overrides().ReflectionMap, Is.SameAs(reflectionData.EnsureTexture2D()));
        //     Assert.That(Overrides().SkyboxTexture, Is.SameAs(skyboxData.EnsureTexture2D()));
        //     Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        // }

        [Test]
        public void PushFogFromPBEnvironmentFogAlone()
        {
            // Arrange
            AddFog(Color.red);

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.SameAs(Component().Environment));
            Assert.That(environment!.FogColor, Is.Not.Null);
            Assert.That(environment.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.red));
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PushFogDensityWhenDirty()
        {
            // Arrange
            world.Add(rootEntity, new PBEnvironmentFog { IsDirty = true, Density = 0.02f });

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
        public void PushSunFromPBSunAlone()
        {
            // Arrange
            world.Add(rootEntity, new PBSun
            {
                IsDirty = true,
                Color = Gradient(Color.green),
                Visible = false,
                Intensity = 2f,
                Direction = new Decentraland.Common.Vector3 { X = 0f, Y = -2f, Z = 0f },
            });

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.SunColor!.Evaluate(0.5f), Is.EqualTo(Color.green));
            Assert.That(environment.SunVisible, Is.False);
            Assert.That(environment.SunIntensity, Is.EqualTo(2f));
            Assert.That(environment.SunDirection, Is.EqualTo(Vector3.down));
            Assert.That(environment.FogColor, Is.Null);
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PushRimColorWhenDirty()
        {
            // Arrange
            world.Add(rootEntity, new PBSkybox
            {
                IsDirty = true,
                SkyColors = new PBSkybox.Types.SkyColors { Rim = Gradient(Color.blue) },
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
        public void CombineAllThreeComponentsIntoOneProfile()
        {
            // Arrange
            world.Add(rootEntity, new PBSkybox { IsDirty = true, Stars = new PBSkybox.Types.Stars { Brightness = 2f } });
            world.Add(rootEntity, new PBSun { IsDirty = true, Intensity = 0.5f });
            AddFog(Color.red);

            // Act
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.StarsBrightness, Is.EqualTo(2f));
            Assert.That(environment.SunIntensity, Is.EqualTo(0.5f));
            Assert.That(environment.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.red));
        }

        [Test]
        public void KeepSameProfileReferenceWhenNotDirty()
        {
            // Arrange
            PBEnvironmentFog pbFog = AddFog(Color.red);
            system.Update(0);
            SceneEnvironmentProfile? environment = Overrides().Environment;

            // Act
            pbFog.IsDirty = false;
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
            PBEnvironmentFog pbFog = AddFog(Color.red);
            system.Update(0);
            SceneEnvironmentProfile? previous = Overrides().Environment;

            // Act
            pbFog.Color = Gradient(Color.blue);
            pbFog.IsDirty = true;
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.Not.SameAs(previous));
            Assert.That(environment!.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.blue));
        }

        [Test]
        public void RebuildProfileWhenOnlyPBSunIsDirty()
        {
            // Arrange
            PBSkybox pbSkybox = AddPbSkybox(SRC_A);
            PBSun pbSun = AddSun(1f);
            system.Update(0);
            Entity promiseEntity = Slot().LoadingPromise!.Value.Entity;
            pbSkybox.IsDirty = false;

            // Act
            pbSun.Intensity = 3f;
            pbSun.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.That(Overrides().Environment!.SunIntensity, Is.EqualTo(3f));
            Assert.That(Slot().LoadingPromise!.Value.Entity, Is.EqualTo(promiseEntity));
        }

        [Test]
        public void RebuildProfileWhenOneOfSeveralComponentsIsRemoved()
        {
            // Arrange: neither remaining component is dirty when the other one goes away
            PBSun pbSun = AddSun(2f);
            PBEnvironmentFog pbFog = AddFog(Color.red);
            system.Update(0);
            SceneEnvironmentProfile? previous = Overrides().Environment;
            pbSun.IsDirty = false;
            pbFog.IsDirty = false;

            // Act
            world.Remove<PBSun>(rootEntity);
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment, Is.Not.SameAs(previous));
            Assert.That(environment!.SunIntensity, Is.Null);
            Assert.That(environment.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.red));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void ReleaseSkyboxTextureButKeepOwnershipWhenOnlyPBSkyboxIsRemoved()
        {
            // Arrange
            AddPbSkybox(SRC_A);
            PBSun pbSun = AddSun(2f);
            system.Update(0);
            TextureData data = ResolvePromise();
            system.Update(0);
            Assert.That(Overrides().SkyboxTexture, Is.SameAs(data.EnsureTexture2D()));
            pbSun.IsDirty = false;

            // Act
            world.Remove<PBSkybox>(rootEntity);
            system.Update(0);

            // Assert
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Slot().TextureData, Is.Null);
            Assert.That(data.CanBeDisposed(), Is.True);
            Assert.That(Overrides().Environment!.SunIntensity, Is.EqualTo(2f));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void PickUpAComponentAddedNextToAnExistingOne()
        {
            // Arrange: the added component is not dirty, only the change of the component set triggers the rebuild
            PBSun pbSun = AddSun(2f);
            system.Update(0);
            pbSun.IsDirty = false;

            // Act
            AddFog(Color.red).IsDirty = false;
            system.Update(0);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment!.SunIntensity, Is.EqualTo(2f));
            Assert.That(environment.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.red));
        }

        [Test]
        public void ClearOverridesWhenAllComponentsAreRemoved()
        {
            // Arrange
            AddSun(2f);
            AddFog(Color.red);
            system.Update(0);
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));

            // Act
            world.Remove<PBSun>(rootEntity);
            world.Remove<PBEnvironmentFog>(rootEntity);
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Component().Environment, Is.Null);
            Assert.That(Component().Sources, Is.EqualTo(SceneSkyboxComponent.EnvironmentSources.None));
        }

        [Test]
        public void ClearEnvironmentOnComponentRemoval()
        {
            // Arrange
            AddFog(Color.red);
            system.Update(0);

            // Act
            world.Remove<PBEnvironmentFog>(rootEntity);
            system.Update(0);

            // Assert
            AssertGlobalCleared();
            Assert.That(Component().Environment, Is.Null);
        }

        [Test]
        public void ClearEnvironmentOnLeaveAndRebuildFromProtoOnCurrent()
        {
            // Arrange
            PBEnvironmentFog pbFog = AddFog(Color.red);
            system.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertGlobalCleared();

            // Arrange: the scene changes the fog and adds a sun while the player is away; the dirty flags are reset before it becomes current again
            pbFog.Color = Gradient(Color.blue);
            pbFog.IsDirty = false;
            AddSun(2f).IsDirty = false;

            // Act
            system.OnSceneIsCurrentChanged(true);

            // Assert
            SceneEnvironmentProfile? environment = Overrides().Environment;
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment!.FogColor!.Evaluate(0.5f), Is.EqualTo(Color.blue));
            Assert.That(environment.SunIntensity, Is.EqualTo(2f));
            Assert.That(Component().Environment, Is.SameAs(environment));
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
        }

        [Test]
        public void ClearEnvironmentOnFinalize()
        {
            // Arrange
            AddFog(Color.red);
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
            SceneEnvironmentProfile? foreignEnvironment = SceneEnvironmentProfile.FromProto(null, null, new PBEnvironmentFog { Color = Gradient(Color.red) });
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
            AddFog(Color.red);
            AddSun(2f);

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
            AddPbSkybox(SRC_A);
            system.Update(0);
            ResolvePromise();

            // Act
            system.Update(0);

            // Assert
            Assert.That(Overrides().Owner, Is.EqualTo(OWN_SCENE));
            Assert.That(Overrides().Environment, Is.Null);
            Assert.That(Component().Environment, Is.Null);
        }

        private static ColorGradient Gradient(Color color) =>
            new ()
            {
                Keys = { new ColorKey { Time = 0f, Color = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a } } },
            };

        private PBSkybox AddPbSkybox(string src)
        {
            var pbSkybox = new PBSkybox { IsDirty = true, SkyboxTexture = src };
            world.Add(rootEntity, pbSkybox);
            return pbSkybox;
        }

        private PBSun AddSun(float intensity)
        {
            var pbSun = new PBSun { IsDirty = true, Intensity = intensity };
            world.Add(rootEntity, pbSun);
            return pbSun;
        }

        private PBEnvironmentFog AddFog(Color color)
        {
            var pbFog = new PBEnvironmentFog { IsDirty = true, Color = Gradient(color) };
            world.Add(rootEntity, pbFog);
            return pbFog;
        }

        private SceneSkyboxComponent Component() =>
            world.Get<SceneSkyboxComponent>(rootEntity);

        private TextureSlot Slot() =>
            Component().SkyboxTexture;

        private ref SceneSkyboxOverrides Overrides() =>
            ref globalWorld.Get<SceneSkyboxOverrides>(globalSkyboxEntity);

        private Texture2D CreateTexture()
        {
            var texture = new Texture2D(2, 2);
            textures.Add(texture);
            return texture;
        }

        private TextureData ResolvePromise()
        {
            var data = new TextureData(CreateTexture());

            // The loading pipeline references the asset once per consumer; the slot releases that reference on clean-up
            data.AcquireRef();

            world.Add(Slot().LoadingPromise!.Value.Entity, new StreamableLoadingResult<TextureData>(data));
            return data;
        }

        private void FailPromise()
        {
            world.Add(Slot().LoadingPromise!.Value.Entity,
                new StreamableLoadingResult<TextureData>(ReportData.UNSPECIFIED, new StreamableLoadingException(LogType.Warning, "simulated failure")));
        }

        private void AssertGlobalCleared()
        {
            Assert.That(Overrides().SkyboxTexture, Is.Null);
            Assert.That(Overrides().Environment, Is.Null);
            Assert.That(Overrides().Owner, Is.Null);
        }
    }
}
