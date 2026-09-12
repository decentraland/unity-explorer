using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.ObjectHighlight
{
    public class HighlightedObjects
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

        public void Highlight(List<Renderer> renderers, in ObjectHighlightSettings settings)
        {
            foreach (Renderer renderer in renderers)
                Highlight(renderer, in settings);
        }

        public void Disparage(Renderer renderer)
        {
            highLightRenderers.Remove(renderer);
        }

        public void Disparage(List<Renderer> renderers)
        {
            foreach (Renderer renderer in renderers)
                Disparage(renderer);
        }

        public void DisparageAll()
        {
            highLightRenderers.Clear();
        }
    }
}
