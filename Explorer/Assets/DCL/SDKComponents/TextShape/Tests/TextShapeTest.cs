using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using DCL.SDKComponents.TextShape.Fonts;

namespace DCL.SDKComponents.TextShape.Tests
{
    public class TextShapeTest
    {
        [Test]
        public void RendererShouldApplyColor()
        {
            var props = new PBTextShape
            {
                TextColor = Color.white.ToColor4(),
            };
            var parent = new GameObject().AddComponent<TextMeshPro>();
            TMP_Text tmp = parent.GetComponentInChildren<TMP_Text>()!;
            parent.Apply(props, new IFontsStorage.Fake(), new MaterialPropertyBlock());

            Assert.AreEqual(tmp.color, props.TextColor.ToUnityColor());
        }
    }
}
