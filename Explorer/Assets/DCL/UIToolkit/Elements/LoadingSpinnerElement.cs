using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UIToolkit.Elements
{
    /// <summary>
    ///     The uGUI LoadingSpinner prefab in UI Toolkit: a track ring and an arc that fills, then empties, every two seconds
    ///     in the prefab's three colours in turn.
    /// </summary>
    [UxmlElement]
    public partial class LoadingSpinnerElement : VisualElement
    {
        private const long TICK_MS = 16;
        private const float CYCLE_SECONDS = 2f; // The prefab's animator state plays the one-second clip at half speed.
        private const float THICKNESS = 0.2f;
        private const float START_ANGLE = 270f; // Painter2D angles run clockwise from 3 o'clock, so the prefab's 12 o'clock start is 270.
        private const float FULL_TURN = 360f;

        private static readonly Color TRACK_COLOR = Color.white;
        private static readonly Color[] CYCLE_COLORS = { new (1f, 0.737f, 0.357f), new (1f, 0.176f, 0.333f), new (0.412f, 0.122f, 0.663f) };

        // One cycle of the prefab's LoadingAnimation keys, with their flat tangents.
        private static readonly AnimationCurve HEAD = new (new Keyframe(0f, 0f), new Keyframe(1f / 3f, 1f), new Keyframe(0.5f, 1f), new Keyframe(5f / 6f, 1f), new Keyframe(0.9833f, 1f), new Keyframe(1f, 0f));
        private static readonly AnimationCurve TAIL = new (new Keyframe(0f, 0f), new Keyframe(1f / 3f, 0f), new Keyframe(0.5f, 0f), new Keyframe(5f / 6f, 1f), new Keyframe(0.9833f, 1f), new Keyframe(1f, 0f));

        private readonly IVisualElementScheduledItem tick;

        private float elapsed;

        public float Head { get; private set; }
        public float Tail { get; private set; }
        public Color ArcColor { get; private set; } = CYCLE_COLORS[0];

        /// <summary>Starts the animation over, or freezes it where it is.</summary>
        public bool IsSpinning
        {
            get => tick.isActive;

            set
            {
                if (value)
                {
                    elapsed = 0f;
                    Advance(0f);
                    tick.Resume();
                }
                else
                    tick.Pause();
            }
        }

        public LoadingSpinnerElement()
        {
            generateVisualContent += OnGenerateVisualContent;
            tick = schedule.Execute(OnTick).Every(TICK_MS);
            tick.Pause();
        }

        /// <summary>Moves the animation on by the given time.</summary>
        public void Advance(float seconds)
        {
            elapsed += seconds;
            int cycle = Mathf.FloorToInt(elapsed / CYCLE_SECONDS);
            float cycleProgress = (elapsed - (cycle * CYCLE_SECONDS)) / CYCLE_SECONDS;
            Head = HEAD.Evaluate(cycleProgress);
            Tail = TAIL.Evaluate(cycleProgress);
            ArcColor = CYCLE_COLORS[cycle % CYCLE_COLORS.Length];
            MarkDirtyRepaint();
        }

        private void OnTick(TimerState state) =>
            Advance(state.deltaTime / 1000f);

        // Both strokes are inset by half their width so the round caps stay inside the box.
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

            if (radius <= 0f)
                return;

            float lineWidth = radius * THICKNESS;
            float arcRadius = radius - (lineWidth * 0.5f);
            Vector2 center = rect.center;

            Painter2D painter = context.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;

            painter.strokeColor = TRACK_COLOR;
            painter.BeginPath();
            painter.Arc(center, arcRadius, 0f, FULL_TURN);
            painter.Stroke();

            if (Head <= Tail)
                return;

            painter.strokeColor = ArcColor;
            painter.BeginPath();
            painter.Arc(center, arcRadius, START_ANGLE + (Tail * FULL_TURN), START_ANGLE + (Head * FULL_TURN));
            painter.Stroke();
        }
    }
}
