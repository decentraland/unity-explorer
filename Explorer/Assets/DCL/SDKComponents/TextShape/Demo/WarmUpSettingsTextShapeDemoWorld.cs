using Arch.Core;
using DCL.Billboard.Demo.Properties;
using DCL.Billboard.Demo.World;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class WarmUpSettingsTextShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;
        private readonly TextShapeProperties textShapeProperties;
        private readonly BillboardProperties billboardProperties;
        private readonly Func<bool> visible;

        private readonly PBTextShape textShape;
        private readonly PBVisibilityComponent visibility;
        private readonly PBBillboard billboard;

        public WarmUpSettingsTextShapeDemoWorld(TextShapeProperties textShapeProperties, BillboardProperties billboardProperties, Func<bool> visible, IFontsStorage fontsStorage)
            : this(new PBTextShape(), new PBVisibilityComponent(), new PBBillboard(), textShapeProperties, billboardProperties, visible, fontsStorage, new ISceneData.Fake()) { }

        public WarmUpSettingsTextShapeDemoWorld(
            PBTextShape textShape,
            PBVisibilityComponent visibility,
            PBBillboard billboard,
            TextShapeProperties textShapeProperties,
            BillboardProperties billboardProperties,
            Func<bool> visible,
            IFontsStorage fontsStorage,
            ISceneData sceneData
        )
        {
            this.textShape = textShape;
            this.visibility = visibility;
            this.billboard = billboard;
            this.textShapeProperties = textShapeProperties;
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
                new TextShapeDemoWorld(
                    world,
                    fontsStorage,
                    sceneData,
                    (textShape, visibility, billboard)
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
            textShapeProperties.ApplyOn(textShape);
            billboardProperties.ApplyOn(billboard);
        }

        private void ApplySettings(PBVisibilityComponent visibilityComponent)
        {
            visibilityComponent.Visible = visible();
            visibilityComponent.IsDirty = true;
        }
    }
}
