using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.Highlight.HighlightedObject
{
    public class LogHighlightedObjects : IHighlightedObjects
    {
        private readonly HashSet<Renderer> highlighted = new ();
        private readonly IHighlightedObjects origin;
        private readonly Action<string> log;

        public LogHighlightedObjects(IHighlightedObjects origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public void Highlight(Renderer renderer, Color color, float thickness)
        {
            highlighted.Add(renderer);
            log($"Highlighting {renderer.name}, currently {highlighted.Count} highlighted objects");
            origin.Highlight(renderer, color, thickness);
        }

        public void Disparage(Renderer renderer)
        {
            highlighted.Remove(renderer);
            log($"Disparage {renderer.name}, currently {highlighted.Count} highlighted objects");
            origin.Disparage(renderer);
        }
    }
}
