using DCL.DebugUtilities.UIBindings;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Sparkline-style line chart driven by a <see cref="LineChartBuffer" />. Auto-scales the Y axis
    ///     to the [min, max] range of the visible samples and strokes the polyline through the
    ///     <see cref="Painter2D" /> exposed by <see cref="MeshGenerationContext" />.
    /// </summary>
    [UxmlElement]
    public partial class DebugLineChartElement
        : DebugElementBase<DebugLineChartElement, DebugLineChartDef>, IBindable, INotifyValueChanged<LineChartBuffer>
    {
        private const string USS_ROOT = "line-chart";
        private const string USS_TITLE = "line-chart__title";
        private const string USS_VALUE = "line-chart__value";
        private const string USS_PLOT = "line-chart__plot";
        private const string PLOT_NAME = "Plot";
        private const string TITLE_NAME = "Title";
        private const string VALUE_NAME = "Value";

        private const float LINE_THICKNESS = 1.5f;

        private Label? titleLabel;
        private Label? valueLabel;
        private VisualElement? plot;

        private LineChartBuffer currentValue;

        public IBinding? binding { get; set; }
        public string? bindingPath { get; set; }

        LineChartBuffer INotifyValueChanged<LineChartBuffer>.value
        {
            get => currentValue;

            set
            {
                currentValue = value;
                UpdateLabels(value);
                plot?.MarkDirtyRepaint();
            }
        }

        public void SetValueWithoutNotify(LineChartBuffer newValue)
        {
            currentValue = newValue;
            UpdateLabels(newValue);
            plot?.MarkDirtyRepaint();
        }

        protected override void ConnectBindings()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            valueLabel = this.Q<Label>(VALUE_NAME);
            plot = this.Q<VisualElement>(PLOT_NAME);

            AddToClassList(USS_ROOT);
            titleLabel?.AddToClassList(USS_TITLE);
            valueLabel?.AddToClassList(USS_VALUE);
            plot?.AddToClassList(USS_PLOT);

            if (titleLabel != null)
                titleLabel.text = definition.Title;

            if (plot != null)
                plot.generateVisualContent += GenerateChart;

            definition.Binding.Connect(this);

            SetValueWithoutNotify(definition.Binding.Value);
        }

        private void UpdateLabels(LineChartBuffer data)
        {
            if (valueLabel == null) return;

            valueLabel.text = data.Count == 0
                ? string.Empty
                : DebugLongMarkerElement.FormatValue((ulong)Mathf.Max(0f, data.DisplayValue), definition.MarkerUnit);
        }

        private static float ToY(float value, float min, float max, float plotHeight, float yPad)
        {
            float t = (value - min) / (max - min);
            return yPad + ((1f - t) * plotHeight);
        }

        private void GenerateChart(MeshGenerationContext mgc)
        {
            if (plot == null) return;

            float[]? buffer = currentValue.Buffer;
            int count = currentValue.Count;

            if (buffer == null || count < 2)
                return;

            Rect rect = plot.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            int capacity = buffer.Length;
            int writeIndex = currentValue.WriteIndex;
            int startIndex = count < capacity ? 0 : writeIndex;

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (var i = 0; i < count; i++)
            {
                float v = buffer[(startIndex + i) % capacity];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (max - min < 1e-3f)
                max = min + 1f;

            float xStep = rect.width / (count - 1);
            float yPad = LINE_THICKNESS;
            float plotHeight = Mathf.Max(0f, rect.height - (yPad * 2f));

            Painter2D painter = mgc.painter2D;
            painter.lineWidth = LINE_THICKNESS;
            painter.strokeColor = definition.LineColor;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, ToY(buffer[startIndex % capacity], min, max, plotHeight, yPad)));

            for (var i = 1; i < count; i++)
            {
                int sampleIndex = (startIndex + i) % capacity;
                float x = i * xStep;
                float y = ToY(buffer[sampleIndex], min, max, plotHeight, yPad);
                painter.LineTo(new Vector2(x, y));
            }

            painter.Stroke();
        }
    }
}
