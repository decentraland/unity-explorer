using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.Properties;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames;
using DCL.Utilities.Extensions;
using Decentraland.Common;
using ECS.Unity.ColorComponent;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.Systems;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class NftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private BillboardProperties billboardProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NftShapeSettings settings = null!;

        private void Start()
        {
            //
            //
            // var world = World.Create();
            //
            // var materialWorld = new MaterialsDemoWorld(world);
            //
            // var pool = new ComponentPoolsRegistry();
            // pool.AddComponentPool<PlanePrimitive>();
            // pool.AddGameObjectPool(MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent);
            //
            // var instantiate = new InstantiatePrimitiveRenderingSystem(
            //     world,
            //     pool,
            //     new FrameTimeCapBudget(),
            //     new ISceneData.Fake()
            // );
            //
            // world.Create(
            //     new PBMeshRenderer
            //     {
            //         Plane = new PBMeshRenderer.Types.PlaneMesh(),
            //     },
            //     new TransformComponent(new GameObject("plane")),
            //     new SDKTransform(),
            //     new PBMaterial()
            //     {
            //         Unlit = new PBMaterial.Types.UnlitMaterial()
            //         {
            //             DiffuseColor = Color.red.ToColor4(),
            //             Texture = new TextureUnion()
            //             {
            //                 Texture = new Decentraland.Common.Texture()
            //                 {
            //                     Src = "urn:decentraland:ethereum:erc721:0x06012c8cf97bead5deae237070f9587f8e7a266d:1631847"
            //                 }
            //             }
            //         }
            //     }
            // );
            //
            // instantiate.Update(0);
            // materialWorld.SetUpAndRunAsync(destroyCancellationToken).Forget();

            new WarmUpSettingsNftShapeDemoWorld(new FramesPool(settings.EnsureNotNull()), nftShapeProperties, billboardProperties, () => visible)
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
