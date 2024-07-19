using Arch.Core;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using System;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class WarmUpSettingsNftShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;
        private readonly NftShapeProperties nftShapeProperties;
        private readonly Func<bool> visible;

        private readonly PBNftShape nftShape;
        private readonly PBVisibilityComponent visibility;

        public WarmUpSettingsNftShapeDemoWorld(IFramesPool framesPool, IReadOnlyFramePrefabs framePrefabs, NftShapeProperties nftShapeProperties, Func<bool> visible) : this(World.Create(), framesPool, framePrefabs, nftShapeProperties, visible) { }

        public WarmUpSettingsNftShapeDemoWorld(World world, IFramesPool framesPool, IReadOnlyFramePrefabs framePrefabs, NftShapeProperties nftShapeProperties, Func<bool> visible) : this(world, framesPool, framePrefabs, new PBNftShape(), new PBVisibilityComponent(), new PBBillboard(), nftShapeProperties, visible) { }

        public WarmUpSettingsNftShapeDemoWorld(
            World world,
            IFramesPool framesPool,
            IReadOnlyFramePrefabs framePrefabs,
            PBNftShape nftShape,
            PBVisibilityComponent visibility,
            PBBillboard billboard,
            NftShapeProperties nftShapeProperties,
            Func<bool> visible
        )
        {
            this.nftShape = nftShape;
            this.visibility = visibility;
            this.nftShapeProperties = nftShapeProperties;
            this.visible = visible;

            this.origin = new SeveralDemoWorld(
                new MaterialsDemoWorld(world),
                new NFTShapeDemoWorld(
                    world,
                    framesPool,
                    framePrefabs,
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
        }

        private void ApplySettings(PBVisibilityComponent visibilityComponent)
        {
            visibilityComponent.Visible = visible();
            visibilityComponent.IsDirty = true;
        }
    }
}
