using Arch.Core;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using DCL.Utilities.Extensions;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class NftShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public NftShapeDemoWorld(World world, params (PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)[] list) : this(world, list.AsReadOnly()) { }

        public NftShapeDemoWorld(World world, IReadOnlyList<(PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)> list)
        {
            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBNftShape nftShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                        w.Create(nftShape, visibility, billboard, NewTransform());
                },
                w => new InstantiateNftShapeSystem(w, new PoolNftShapeRendererFactory(new ComponentPoolsRegistry())),
                w => new VisibilityNftShapeSystem(w)
            );
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("nft test"));

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();
    }
}
