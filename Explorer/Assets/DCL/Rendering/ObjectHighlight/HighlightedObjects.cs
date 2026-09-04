using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.ObjectHighlight
{
    public class HighlightedObjects : IHighlightedObjects
    {
        private readonly Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers;

        public HighlightedObjects(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers)
        {
            this.highLightRenderers = highLightRenderers;
        }

        public void Highlight(Renderer renderer, in ObjectHighlightSettings settings)
        {
            highLightRenderers[renderer] = settings;
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
