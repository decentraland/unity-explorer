using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using System;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Renderer.Factory
{
    public class TextShapeRendererFactory : ITextShapeRendererFactory
    {
        private readonly PBTextShape textShape = Default();
        private readonly IFontsStorage fontsStorage;

        public TextShapeRendererFactory(IFontsStorage fontsStorage)
        {
            this.fontsStorage = fontsStorage;
        }

        public ITextShapeRenderer New(Transform parent)
        {
            var text = new GameObject($"text component: {HashCode.Combine(parent.GetHashCode(), parent.childCount)}");
            text.transform.SetParent(parent);
            var tmp = text.AddComponent<TextMeshPro>()!;
            var renderer = new TMPTextShapeRenderer(tmp, fontsStorage);
            renderer.Apply(textShape);
            return renderer;
        }

        private static PBTextShape Default()
        {
            var v = new PBTextShape();
            new TextShapeProperties().ApplyOn(v);
            return v;
        }
    }
}
