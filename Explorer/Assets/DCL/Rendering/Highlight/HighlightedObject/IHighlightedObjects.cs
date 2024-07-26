using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.Highlight.HighlightedObject
{
    public interface IHighlightedObjects
    {
        void Highlight(Renderer renderer, Color color, float thickness);

        void Disparage(Renderer renderer);

        void DisparageAll();
    }

    public static class HighlightedObjectsExtensions
    {
        public static void Highlight(this IHighlightedObjects highlightedObjects, IEnumerable<Renderer> renderers, Color color, float thickness)
        {
            foreach (Renderer renderer in renderers)
                highlightedObjects.Highlight(renderer, color, thickness);
        }

        public static void Disparage(this IHighlightedObjects highlightedObjects, IEnumerable<Renderer> renderers)
        {
            foreach (Renderer renderer in renderers)
                highlightedObjects.Disparage(renderer);
        }
    }
}
