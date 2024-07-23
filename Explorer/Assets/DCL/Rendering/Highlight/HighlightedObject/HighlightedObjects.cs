using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.Highlight.HighlightedObject
{
    public class HighlightedObjects : IHighlightedObjects
    {
        private readonly Dictionary<Renderer, HighlightSettings> m_HighLightRenderers;

        public HighlightedObjects(Dictionary<Renderer, HighlightSettings> highLightRenderers)
        {
            m_HighLightRenderers = highLightRenderers;
        }

        public void Highlight(Renderer renderer, Color color, float thickness)
        {
            if (!m_HighLightRenderers.ContainsKey(renderer))
            {
                m_HighLightRenderers.Add(renderer, new HighlightSettings
                {
                    Color = color,
                    Width = thickness,
                });
            }
            else
            {
                HighlightSettings highlightSettings = m_HighLightRenderers[renderer];
                highlightSettings.Color = color;
                highlightSettings.Width = thickness;
                m_HighLightRenderers[renderer] = highlightSettings;
            }
        }

        public void Disparage(Renderer renderer)
        {
            if (m_HighLightRenderers.ContainsKey(renderer))
                m_HighLightRenderers.Remove(renderer);
        }
    }
}
