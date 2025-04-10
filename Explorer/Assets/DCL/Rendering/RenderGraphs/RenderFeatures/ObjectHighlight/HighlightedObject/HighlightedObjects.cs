using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public class ObjectHighlight : IObjectHighlight
    {
        private readonly Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers;

        public ObjectHighlight(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers)
        {
            this.highLightRenderers = highLightRenderers;
        }

        public void Highlight(Renderer renderer, Color color, float thickness)
        {
            highLightRenderers[renderer] = new ObjectHighlightSettings
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
