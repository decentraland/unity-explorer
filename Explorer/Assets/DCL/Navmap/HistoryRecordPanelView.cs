using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class HistoryRecordPanelView : MonoBehaviour
    {
        public event Action<string> OnClickedHistoryRecord;

        [field: SerializeField]
        public HistoryRecordView[] HistoryRecordView { get; private set; }

        private void Awake() =>
            ResetRecords();

        //Working with a predefined set of records considering that in total
        //it is only 5 elements and a pool would be an overkill
        public void SetHistoryRecords(string[] historyRecords)
        {
            ResetRecords();

            for (var i = 0; i < historyRecords.Length; i++)
            {
                HistoryRecordView[i].OnClickedHistoryRecord += OnClickedHistoryRecord;
                HistoryRecordView[i].gameObject.SetActive(true);
                HistoryRecordView[i].historyText.text = historyRecords[i];
            }
        }

        private void ResetRecords()
        {
            foreach (var recordView in HistoryRecordView)
            {
                recordView.OnClickedHistoryRecord -= OnClickedHistoryRecord;
                recordView.gameObject.SetActive(false);
            }
        }
    }
}
