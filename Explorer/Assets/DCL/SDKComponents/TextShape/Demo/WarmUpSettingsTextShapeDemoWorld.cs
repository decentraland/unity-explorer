using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using System;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class WarmUpSettingsTextShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;
        private readonly TextShapeProperties textShapeProperties;
        private readonly Func<bool> visible;

        private readonly PBTextShape textShape;
        private readonly PBVisibilityComponent visibility;

        public WarmUpSettingsTextShapeDemoWorld(TextShapeProperties textShapeProperties, Func<bool> visible) : this(new PBTextShape(), new PBVisibilityComponent(), textShapeProperties, visible) { }

        public WarmUpSettingsTextShapeDemoWorld(PBTextShape textShape, PBVisibilityComponent visibility, TextShapeProperties textShapeProperties, Func<bool> visible)
        {
            this.textShape = textShape;
            this.visibility = visibility;
            this.textShapeProperties = textShapeProperties;
            this.visible = visible;
            this.origin = new TextShapeDemoWorld((textShape, visibility));
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
        }

        private void ApplySettings(PBVisibilityComponent visibilityComponent)
        {
            visibilityComponent.Visible = visible();
            visibilityComponent.IsDirty = true;
        }
    }
}
