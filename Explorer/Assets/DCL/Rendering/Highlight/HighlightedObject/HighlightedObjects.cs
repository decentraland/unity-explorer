using System;
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
            if (!highLightRenderers.ContainsKey(renderer))
            {
                highLightRenderers.Add(renderer, new HighlightSettings
                {
                    Color = color,
                    Width = thickness,
                });
            }
            else
            {
                HighlightSettings highlightSettings = highLightRenderers[renderer];
                highlightSettings.Color = color;
                highlightSettings.Width = thickness;
                highLightRenderers[renderer] = highlightSettings;
            }
        }

        public void Disparage(Renderer renderer)
        {
            if (highLightRenderers.ContainsKey(renderer))
                highLightRenderers.Remove(renderer);
        }

        public void DisparageAll()
        {
            highLightRenderers.Clear();
        }
    }
}
