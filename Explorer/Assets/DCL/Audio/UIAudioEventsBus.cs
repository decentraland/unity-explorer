using System;

namespace DCL.Audio
{
    public enum UIAudioType
    {
        BUTTON,
        HOVER,
    }

    public interface IUIAudioEventsBus
    {
        public event Action<UIAudioType> AudioEvent;
        public void SendAudioEvent(UIAudioType type);
    }

    public class UIAudioEventsBus : IDisposable, IUIAudioEventsBus
    {
        public event Action<UIAudioType> AudioEvent;

        public void SendAudioEvent(UIAudioType type)
        {
            AudioEvent?.Invoke(type);
        }

        public void Dispose()
        { }

    }
}
