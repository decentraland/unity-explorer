using Arch.Core;
using DCL.Billboard.Demo.Properties;
using DCL.Billboard.Demo.World;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
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

        public WarmUpSettingsNftShapeDemoWorld(NftShapeProperties nftShapeProperties, BillboardProperties billboardProperties, Func<bool> visible) : this(new PBNftShape(), new PBVisibilityComponent(), new PBBillboard(), nftShapeProperties, billboardProperties, visible) { }

        public WarmUpSettingsNftShapeDemoWorld(
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

            var world = World.Create();

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
