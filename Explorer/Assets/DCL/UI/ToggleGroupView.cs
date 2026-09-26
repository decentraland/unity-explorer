using DCL.Audio;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    [RequireComponent(typeof(ToggleGroup))]
    public class ToggleGroupView : MonoBehaviour
    {
        [SerializeField] private List<Toggle> toggles = null!;
        [SerializeField] private AudioClipConfig togglePressedAudio = null!;

        public int SelectedToggleIndex
        {
            get
            {
                for (var i = 0; i < toggles.Count; i++)
                {
                    if (toggles[i].isOn)
                        return i;
                }

                return -1;
            }

            set
            {
                if (value < 0 || value >= toggles.Count)
                    return;

                for (var i = 0; i < toggles.Count; i++)
                    toggles[i].SetIsOnWithoutNotify(i == value);
            }
        }

        private void Awake()
        {
            GetComponent<ToggleGroup>().allowSwitchOff = false;

            foreach (Toggle toggle in toggles)
                toggle.onValueChanged.AddListener(isOn => UIAudioEventsBus.Instance.SendPlayAudioEvent(togglePressedAudio));
        }

        private void OnDestroy()
        {
            foreach (Toggle toggle in toggles)
                toggle.onValueChanged.RemoveAllListeners();
        }

        public void SetAsInteractable(bool isInteractable)
        {
            foreach (Toggle toggle in toggles)
                toggle.interactable = isInteractable;
        }
    }
}
