using DCL.DebugUtilities.UIBindings;
using UnityEngine;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for a sparkline-style line chart that plots a rolling buffer of float samples.
    ///     The displayed current-value label is formatted using the same units as
    ///     <see cref="DebugLongMarkerDef" />.
    /// </summary>
    public class DebugLineChartDef : IDebugElementDef
    {
        public readonly ElementBinding<LineChartBuffer> Binding;
        public readonly string Title;
        public readonly Color LineColor;
        public readonly DebugLongMarkerDef.Unit MarkerUnit;

        public DebugLineChartDef(ElementBinding<LineChartBuffer> binding, string title, Color lineColor, DebugLongMarkerDef.Unit markerUnit = DebugLongMarkerDef.Unit.NoFormat)
        {
            Binding = binding;
            Title = title;
            LineColor = lineColor;
            MarkerUnit = markerUnit;
        }
    }
}
