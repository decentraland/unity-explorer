using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.OTPInput
{
    public class OTPSlotView : MonoBehaviour
    {
        public enum SlotState
        {
            UNSELECTED,
            SELECTED,
            ERROR,
            SUCCESS,
        }

        [SerializeField] private TMP_Text text;
        [SerializeField] private Image background;
        [SerializeField] private Image outline;

        [SerializeField] private Color normalColor = new (0.15f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color successColor = new (0.2f, 0.7f, 0.4f, 1f);
        [SerializeField] private Color errorColor = new (0.8f, 0.3f, 0.3f, 1f);

        public Vector3 Center => transform.position;

        public string Text
        {
            set => text.text = value;
        }

        private void Awake()
        {
            SetState(SlotState.UNSELECTED);
        }

        public void SetState(SlotState state)
        {
            outline.gameObject.SetActive(state == SlotState.SELECTED);

            background.color = state switch
                               {
                                   SlotState.UNSELECTED or SlotState.SELECTED => normalColor,
                                   SlotState.ERROR => errorColor,
                                   SlotState.SUCCESS => successColor,
                                   _ => background.color,
                               };
        }
    }
}
