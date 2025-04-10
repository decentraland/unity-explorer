using System;
using System.Collections.Generic;
using UnityEngine;
using DCL.Diagnostics;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public class LogObjectHighlight : IObjectHighlight
    {
        private readonly HashSet<Renderer> highlighted = new ();
        private readonly IObjectHighlight origin;

        public LogObjectHighlight(IObjectHighlight origin)
        {
            this.origin = origin;
        }

        public void Highlight(Renderer renderer, Color color, float thickness)
        {
            highlighted.Add(renderer);
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"Highlighting {renderer.name}, currently {highlighted.Count} highlighted objects");
            origin.Highlight(renderer, color, thickness);
        }

        public void Disparage(Renderer renderer)
        {
            highlighted.Remove(renderer);
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"Disparage {renderer.name}, currently {highlighted.Count} highlighted objects");
            origin.Disparage(renderer);
        }

        public void DisparageAll()
        {
            origin.DisparageAll();
        }
    }
}
