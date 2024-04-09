using CommunicationData.URLHelpers;
using CRDT;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Materials.Systems;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility.Primitives;
using Entity = Arch.Core.Entity;
using MaterialTransparencyMode = DCL.ECSComponents.MaterialTransparencyMode;
using Texture = Decentraland.Common.Texture;
using TextureWrapMode = Decentraland.Common.TextureWrapMode;

namespace ECS.Unity.Materials.Tests
{
    public class StartMaterialLoadingSystemShould : UnitySystemTestBase<StartMaterialsLoadingSystem>
    {
        private const int ATTEMPTS_COUNT = 5;

        private ISceneData sceneData;
        private DestroyMaterial destroyMaterial;

        private static string tex1 => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private static string tex2 => $"file://{Application.dataPath + "/../TestResources/Images/atlas.png"}";
        private static string tex3 => $"file://{Application.dataPath + "/../TestResources/Images/Gradient A4.png"}";


        public void SetUp()
        {
            IReleasablePerformanceBudget releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();
            releasablePerformanceBudget.TrySpendBudget().Returns(true);

            system = new StartMaterialsLoadingSystem(world,
                destroyMaterial = Substitute.For<DestroyMaterial>(),
                sceneData = Substitute.For<ISceneData>(), ATTEMPTS_COUNT, releasablePerformanceBudget, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>()
                , new ExtendedObjectPool<Texture2D>(() => new Texture2D(1, 1)));

            sceneData.TryGetMediaUrl(Arg.Any<string>(), out Arg.Any<URLAddress>())
                     .Returns(c =>
                      {
                          c[1] = URLAddress.FromString(c.ArgAt<string>(0));
                          return true;
                      });
        }


        public void CreatePBRComponent()
        {
            PBMaterial material = CreatePBRMaterial1();

            Entity e = world.Create(material, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out MaterialComponent materialComponent));

            Assert.AreEqual(StreamableLoading.LifeCycle.LoadingInProgress, materialComponent.Status);
            Assert.IsNull(materialComponent.Result);

            Assert.IsTrue(materialComponent.Data.IsPbrMaterial);

            AssertPBRMaterial(material, materialComponent);
        }


        public void CreateBasicComponent()
        {
            PBMaterial basic = CreateBasicMaterial();

            Entity e = world.Create(basic, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out MaterialComponent materialComponent));

            Assert.AreEqual(StreamableLoading.LifeCycle.LoadingInProgress, materialComponent.Status);
            Assert.IsNull(materialComponent.Result);

            Assert.IsFalse(materialComponent.Data.IsPbrMaterial);

            AssertBasicMaterial(basic, materialComponent);
        }

        private static void AssertBasicMaterial(PBMaterial expected, MaterialComponent actual)
        {
            Assert.AreEqual(expected.GetAlphaTest(), actual.Data.AlphaTest);
            Assert.AreEqual(expected.GetDiffuseColor(), actual.Data.DiffuseColor);
            Assert.AreEqual(expected.GetCastShadows(), actual.Data.CastShadows);

            AssertTextureComponent(expected.Unlit.Texture, actual.Data.AlbedoTexture);
        }


        public void KeepMaterialIfComponentNotChanged()
        {
            PBMaterial material1 = CreatePBRMaterial1();

            Entity e = world.Create(material1, PartitionComponent.TOP_PRIORITY);

            // First run -> create material component

            system.Update(0);

            PBMaterial material2 = CreatePBRMaterial1();
            material2.IsDirty = true;
            world.Set(e, material2);

            ref MaterialComponent c = ref world.Get<MaterialComponent>(e);
            c.Result = DefaultMaterial.New();
            c.Status = StreamableLoading.LifeCycle.LoadingFinished;

            // Second run -> keep material component

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out MaterialComponent materialComponent));

            // the same material component data
            AssertPBRMaterial(material2, materialComponent);
            AssertPBRMaterial(material1, materialComponent);

            destroyMaterial.DidNotReceive()(Arg.Any<MaterialData>(), Arg.Any<Material>());
        }


        public void ChangeFromPBRToBasic()
        {
            PBMaterial material1 = CreatePBRMaterial1();

            Entity e = world.Create(material1, PartitionComponent.TOP_PRIORITY);

            // First run -> create material component

            system.Update(0);

            PBMaterial material2 = CreateBasicMaterial();
            material2.IsDirty = true;
            world.Set(e, material2);

            ref MaterialComponent c = ref world.Get<MaterialComponent>(e);
            c.Result = DefaultMaterial.New();
            c.Status = StreamableLoading.LifeCycle.LoadingFinished;

            MaterialData dataCopy = c.Data;

            // Second run -> return material to cache

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out MaterialComponent materialComponent));
            AssertBasicMaterial(material2, materialComponent);

            destroyMaterial.Received(1)(dataCopy, Arg.Any<Material>());
        }


        public void StartBasicLoading()
        {
            PBMaterial sdkComponent = CreateBasicMaterial();

            Entity e = world.Create(sdkComponent, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingInProgress));

            AssertTexturePromise(in afterUpdate.AlbedoTexPromise, tex2);
        }


        public void StartPBRLoading()
        {
            PBMaterial sdkComponent = CreatePBRMaterial1();

            Entity e = world.Create(sdkComponent, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingInProgress));

            AssertTexturePromise(afterUpdate.AlbedoTexPromise, tex1);
            AssertTexturePromise(afterUpdate.AlphaTexPromise, tex3);
            AssertTexturePromise(afterUpdate.EmissiveTexPromise, tex1);
            AssertTexturePromise(afterUpdate.BumpTexPromise, tex2);
        }


        public void AbortRequestIfMaterialChanged()
        {
            PBMaterial material1 = CreatePBRMaterial1();

            Entity e = world.Create(material1, PartitionComponent.TOP_PRIORITY);

            // First run -> create material component

            system.Update(0);

            PBMaterial material2 = CreateBasicMaterial();
            material2.IsDirty = true;
            world.Set(e, material2);

            ref MaterialComponent c = ref world.Get<MaterialComponent>(e);
            c.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            // Add entity reference
            var texPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention { CommonArguments = new CommonLoadingArguments("URL") }, PartitionComponent.TOP_PRIORITY);
            c.AlphaTexPromise = texPromise;

            // Second run -> release promise

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out MaterialComponent materialComponent));
            AssertBasicMaterial(material2, materialComponent);

            Assert.IsTrue(texPromise.LoadingIntention.CommonArguments.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(materialComponent.AlphaTexPromise.HasValue);
        }

        private void AssertTexturePromise(in AssetPromise<Texture2D, GetTextureIntention>? promise, string src)
        {
            Assert.That(promise.HasValue, Is.True);
            AssetPromise<Texture2D, GetTextureIntention> promiseValue = promise.Value;

            Assert.That(world.TryGet(promiseValue.Entity, out GetTextureIntention intention), Is.True);
            Assert.That(intention.CommonArguments.URL, Is.EqualTo(src));
            Assert.That(intention.CommonArguments.Attempts, Is.EqualTo(ATTEMPTS_COUNT));
        }

        private static PBMaterial CreatePBRMaterial1()
        {
            var material = new PBMaterial
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    Texture = new TextureUnion
                    {
                        Texture = new Texture
                        {
                            Src = tex1,
                            WrapMode = TextureWrapMode.TwmMirror,
                            FilterMode = TextureFilterMode.TfmPoint,
                        },
                    },
                    BumpTexture = new TextureUnion
                    {
                        Texture = new Texture
                        {
                            Src = tex2,
                            WrapMode = TextureWrapMode.TwmClamp,
                            FilterMode = TextureFilterMode.TfmBilinear,
                        },
                    },
                    AlphaTexture = new TextureUnion
                    {
                        Texture = new Texture
                        {
                            Src = tex3,
                            WrapMode = TextureWrapMode.TwmRepeat,
                            FilterMode = TextureFilterMode.TfmTrilinear,
                        },
                    },
                    EmissiveTexture = new TextureUnion
                    {
                        Texture = new Texture
                        {
                            Src = tex1,
                            WrapMode = TextureWrapMode.TwmMirror,
                            FilterMode = TextureFilterMode.TfmBilinear,
                        },
                    },
                    AlphaTest = 0.5f,
                    CastShadows = true,
                    AlbedoColor = new Color4 { R = 0.1f, G = 0.2f, B = 0.3f, A = 0.4f },
                    EmissiveColor = new Color3 { R = 0.5f, G = 0.6f, B = 0.7f },
                    ReflectivityColor = new Color3 { R = 0.8f, G = 0.9f, B = 1.0f },
                    TransparencyMode = MaterialTransparencyMode.MtmAlphaBlend,
                    Metallic = 0.1f,
                    Roughness = 0.2f,
                    SpecularIntensity = 0.5f,
                    EmissiveIntensity = 0,
                    DirectIntensity = 0.3f,
                },
            };

            return material;
        }

        private static void AssertTextureComponent(TextureUnion expected, TextureComponent? actualNullable)
        {
            if (expected == null)
            {
                Assert.IsNull(actualNullable);
                return;
            }

            Assert.IsNotNull(actualNullable);

            TextureComponent actual = actualNullable.Value;

            Assert.AreEqual(expected.Texture.Src, actual.Src);
            Assert.AreEqual(expected.Texture.WrapMode.ToUnityWrapMode(), actual.WrapMode);
            Assert.AreEqual(expected.Texture.FilterMode.ToUnityFilterMode(), actual.FilterMode);
        }

        private static void AssertPBRMaterial(PBMaterial expected, MaterialComponent actual)
        {
            Assert.AreEqual(expected.GetAlphaTest(), actual.Data.AlphaTest);
            Assert.AreEqual(expected.GetCastShadows(), actual.Data.CastShadows);
            Assert.AreEqual(expected.GetAlbedoColor(), actual.Data.AlbedoColor);
            Assert.AreEqual(expected.GetEmissiveColor(), actual.Data.EmissiveColor);
            Assert.AreEqual(expected.GetReflectiveColor(), actual.Data.ReflectivityColor);
            Assert.AreEqual(expected.GetTransparencyMode(), actual.Data.TransparencyMode);
            Assert.AreEqual(expected.GetMetallic(), actual.Data.Metallic);
            Assert.AreEqual(expected.GetRoughness(), actual.Data.Roughness);
            Assert.AreEqual(expected.GetSpecularIntensity(), actual.Data.SpecularIntensity);
            Assert.AreEqual(expected.GetEmissiveColor(), actual.Data.EmissiveColor);
            Assert.AreEqual(expected.GetDirectIntensity(), actual.Data.DirectIntensity);

            AssertTextureComponent(expected.Pbr.Texture, actual.Data.AlbedoTexture);
            AssertTextureComponent(expected.Pbr.BumpTexture, actual.Data.BumpTexture);
            AssertTextureComponent(expected.Pbr.AlphaTexture, actual.Data.AlphaTexture);
            AssertTextureComponent(expected.Pbr.EmissiveTexture, actual.Data.EmissiveTexture);
        }

        private static PBMaterial CreateBasicMaterial()
        {
            var material = new PBMaterial
            {
                Unlit = new PBMaterial.Types.UnlitMaterial
                {
                    Texture = new TextureUnion
                    {
                        Texture = new Texture
                        {
                            Src = tex2,
                            WrapMode = TextureWrapMode.TwmMirror,
                            FilterMode = TextureFilterMode.TfmBilinear,
                        },
                    },
                    AlphaTest = 0.6f,
                    CastShadows = false,
                    DiffuseColor = new Color4 { R = 0.1f, G = 0.2f, B = 0.3f, A = 0.4f },
                },
            };

            return material;
        }
    }
}
