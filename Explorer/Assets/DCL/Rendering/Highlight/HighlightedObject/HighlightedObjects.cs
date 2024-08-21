using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.Highlight.HighlightedObject
{
    public class HighlightedObjects : IHighlightedObjects
    {
        private readonly Dictionary<Renderer, HighlightSettings> highLightRenderers;

        public HighlightedObjects(Dictionary<Renderer, HighlightSettings> highLightRenderers)
        {
            this.highLightRenderers = highLightRenderers;
        }

        public void Highlight(Renderer renderer, Color color, float thickness)
        {
            highLightRenderers[renderer] = new HighlightSettings
            {
                Color = color,
                Width = thickness,
            };
        }

        public void Disparage(Renderer renderer)
        {
            highLightRenderers.Remove(renderer);
        }

        public void DisparageAll()
        {
            highLightRenderers.Clear();
        }
    }
}
