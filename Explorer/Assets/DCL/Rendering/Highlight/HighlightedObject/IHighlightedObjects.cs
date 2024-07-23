using UnityEngine;

namespace DCL.Rendering.Highlight.HighlightedObject
{
    public interface IHighlightedObjects
    {
        void Highlight(Renderer renderer, Color color, float thickness);

        void Disparage(Renderer renderer);
    }
}
