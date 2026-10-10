using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class HistoryRecordView : MonoBehaviour
    {
        public event Action<string> OnClickedHistoryRecord;

        [field: SerializeField]
        public TMP_Text historyText;

        [field: SerializeField]
        public Button button;

        private void Start() =>
            button.onClick.AddListener(() => OnClickedHistoryRecord?.Invoke(historyText.text));
    }
}
