using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    /// <summary>
    ///     Reusable prev / index / next stepper. Prev and next arrows emit a delta of -1 / +1 on
    ///     <see cref="OnCycle"/>. Storage, wrap and bounds live in <see cref="NumericCyclerController"/>.
    /// </summary>
    public class NumericCyclerView : MonoBehaviour
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
