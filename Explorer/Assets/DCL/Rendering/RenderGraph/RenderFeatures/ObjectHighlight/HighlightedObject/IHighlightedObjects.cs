using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.RenderGraph.RenderFeatures.ObjectHighlight
{
    public interface IObjectHighlight
    {
        void Highlight(Renderer renderer, Color color, float thickness);

        void Disparage(Renderer renderer);

        void DisparageAll();
    }

    public static class ObjectHighlightExtensions
    {
        public static void Highlight(this IObjectHighlight highlightedObjects, IEnumerable<Renderer> renderers, Color color, float thickness)
        {
            foreach (Renderer renderer in renderers)
                highlightedObjects.Highlight(renderer, color, thickness);
        }

        public static void Disparage(this IObjectHighlight highlightedObjects, IEnumerable<Renderer> renderers)
        {
            foreach (Renderer renderer in renderers)
                highlightedObjects.Disparage(renderer);
        }
    }
}
