using Arch.Core;
using DCL.Billboard.Demo.Properties;
using DCL.Billboard.Demo.World;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames.Pool;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class WarmUpSettingsNftShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;
        private readonly NftShapeProperties nftShapeProperties;
        private readonly BillboardProperties billboardProperties;
        private readonly Func<bool> visible;

        private readonly PBNftShape nftShape;
        private readonly PBVisibilityComponent visibility;
        private readonly PBBillboard billboard;

        public WarmUpSettingsNftShapeDemoWorld(IFramesPool framesPool, NftShapeProperties nftShapeProperties, BillboardProperties billboardProperties, Func<bool> visible) : this(World.Create(), framesPool, nftShapeProperties, billboardProperties, visible) { }

        public WarmUpSettingsNftShapeDemoWorld(World world, IFramesPool framesPool, NftShapeProperties nftShapeProperties, BillboardProperties billboardProperties, Func<bool> visible) : this(world, framesPool, new PBNftShape(), new PBVisibilityComponent(), new PBBillboard(), nftShapeProperties, billboardProperties, visible) { }

        public WarmUpSettingsNftShapeDemoWorld(
            World world,
            IFramesPool framesPool,
            PBNftShape nftShape,
            PBVisibilityComponent visibility,
            PBBillboard billboard,
            NftShapeProperties nftShapeProperties,
            BillboardProperties billboardProperties,
            Func<bool> visible
        )
        {
            this.nftShape = nftShape;
            this.visibility = visibility;
            this.billboard = billboard;
            this.nftShapeProperties = nftShapeProperties;
            this.billboardProperties = billboardProperties;
            this.visible = visible;

            this.origin = new SeveralDemoWorld(
                new BillboardDemoWorld(
                    world,
                    Vector3.zero,
                    randomCounts: 0,
                    predefinedBillboards: Array.Empty<BillboardMode>()
                ),
                new MaterialsDemoWorld(world),
                new NftShapeDemoWorld(
                    world,
                    framesPool,
                    (nftShape, visibility, billboard)
                )
            );
        }

        public void SetUp()
        {
            ApplySettings();
            origin.SetUp();
        }

        public void Update()
        {
            ApplySettings();
            origin.Update();
        }

        private void ApplySettings()
        {
            ApplySettings(visibility);
            nftShapeProperties.ApplyOn(nftShape);
            billboardProperties.ApplyOn(billboard);
        }

        private void ApplySettings(PBVisibilityComponent visibilityComponent)
        {
            visibilityComponent.Visible = visible();
            visibilityComponent.IsDirty = true;
        }
    }
}
