using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.FacialExpressionsWheel
{
    /// <summary>
    ///     One row of the wheel footer (EYEBROWS / EYES / MOUTH). Hosts a label plus prev/next arrows
    ///     to step through that channel's atlas slices, and a "current/total" index display.
    ///     The view is dumb: clamping and bounds live in the controller. Label is authored in the prefab.
    /// </summary>
    public class FaceChannelCyclerView : MonoBehaviour
    {
        public event Action<int>? OnCycle;

        [field: SerializeField]
        public TMP_Text IndexText { get; private set; } = null!;

        [SerializeField]
        private Button previousButton = null!;

        [SerializeField]
        private Button nextButton = null!;

        private void Awake()
        {
            previousButton.onClick.AddListener(() => OnCycle?.Invoke(-1));
            nextButton.onClick.AddListener(() => OnCycle?.Invoke(+1));
        }

        public void SetIndex(int currentOneBased, int total) =>
            IndexText.text = $"{currentOneBased}/{total}";
    }
}