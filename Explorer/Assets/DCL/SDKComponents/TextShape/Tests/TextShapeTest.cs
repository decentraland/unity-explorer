using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Tests
{
    public class TextShapeTest
    {
        [Test]
        public void TMPTextRendererColoring()
        {
            var props = new PBTextShape
            {
                TextColor = Color.white.ToColor4(),
            };
            var parent = new GameObject().transform;
            // var renderer = new TextShapeRendererFactory(new IFontsStorage.Fake()).New(parent);
            var tmp = parent.GetComponentInChildren<TMP_Text>()!;

            // renderer.Apply(props);

            Assert.AreEqual(tmp.color, props.TextColor.ToUnityColor());
        }
    }
}
