using DCL.Diagnostics;
using System;

namespace DCL.Audio
{
    public enum UIAudioType
    {
        GENERIC_TOGGLE_ON = 0,
        GENERIC_TOGGLE_OFF = 1,
        GENERIC_TAB_SELECTED = 2,
        GENERIC_INPUT_TEXT = 3,
        GENERIC_INPUT_CLEAR_TEXT = 4,
        GENERIC_DROPDOWN = 5,
        GENERIC_BUTTON = 6,
        CHAT_SEND_MESSAGE = 100,
        CHAT_RECEIVE_MESSAGE = 101,
        CHAT_CLOSE = 102,
        CHAT_INPUT_SELECTED = 103,
        CHAT_INPUT_DESELECTED = 104,
        CHAT_ADD_EMOJI = 105,
        CHAT_OPEN_EMOJI_PANEL = 106,
        BACKPACK_UNEQUIP_WEARABLE = 200,
        BACKPACK_EQUIP_WEARABLE = 201,
        BACKPACK_CHANGE_TAB = 202,
    }

    public class UIAudioEventsBus : IDisposable
    {
        private static UIAudioEventsBus instance;

        public static UIAudioEventsBus Instance
        {
            get
            {
                return instance ??= new UIAudioEventsBus();
            }
        }

        public event Action<AudioClipConfig> AudioEvent;

        public void Dispose() { }

        public void SendAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { AudioEvent?.Invoke(audioClipConfig); }
        }
    }
}
