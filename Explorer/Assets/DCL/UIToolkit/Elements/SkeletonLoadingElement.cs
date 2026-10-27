using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UIToolkit.Elements
{
    /// <summary>
    ///     The uGUI SkeletonLoadingView in UI Toolkit: a row of bones, each with a gradient sweeping across it once a second;
    ///     their footprint and colours come from the panel's stylesheet.
    /// </summary>
    [UxmlElement]
    public partial class SkeletonLoadingElement : VisualElement
    {
        private const string USS_BLOCK = "skeleton-loading";
        private const string USS_BONE = USS_BLOCK + "__bone";
        private const string USS_SHIMMER = USS_BLOCK + "__shimmer";
        private const long TICK_MS = 16;
        private const float CYCLE_SECONDS = 1f;

        // The gradient is twice the bone's width (see the stylesheet), so its middle fills the bone mid-sweep instead of streaking past
        private const float SHIMMER_START_PERCENT = -200f;
        private const float SHIMMER_END_PERCENT = 100f;

        private readonly List<VisualElement> shimmers = new ();
        private readonly IVisualElementScheduledItem tick;

        private int boneCount;
        private float elapsed;

        /// <summary>The sweep so far, from 0 off the left edge to 1 off the right one.</summary>
        public float Phase { get; private set; }

        [UxmlAttribute]
        public int BoneCount
        {
            get => boneCount;

            set
            {
                if (value == boneCount) return;

                boneCount = value;
                RebuildBones();
            }
        }

        /// <summary>Starts the sweep over, or freezes it where it is.</summary>
        public bool IsAnimating
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

        public SkeletonLoadingElement()
        {
            AddToClassList(USS_BLOCK);
            tick = schedule.Execute(OnTick).Every(TICK_MS);
            tick.Pause();
        }

        public void Advance(float seconds)
        {
            elapsed += seconds;
            Phase = Mathf.Repeat(elapsed, CYCLE_SECONDS) / CYCLE_SECONDS;
            Length left = Length.Percent(Mathf.Lerp(SHIMMER_START_PERCENT, SHIMMER_END_PERCENT, Phase));

            for (var i = 0; i < shimmers.Count; i++)
                shimmers[i].style.left = left;
        }

        private void OnTick(TimerState state) =>
            Advance(state.deltaTime / 1000f);

        private void RebuildBones()
        {
            Clear();
            shimmers.Clear();

            for (var i = 0; i < boneCount; i++)
            {
                var bone = new VisualElement { pickingMode = PickingMode.Ignore };
                bone.AddToClassList(USS_BONE);
                var shimmer = new VisualElement { pickingMode = PickingMode.Ignore };
                shimmer.AddToClassList(USS_SHIMMER);
                bone.Add(shimmer);
                Add(bone);
                shimmers.Add(shimmer);
            }

            Advance(0f);
        }
    }
}
