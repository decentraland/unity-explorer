using System;

namespace DCL.Audio
{
    public class WorldAudioEventsBus : IDisposable
    {
        private static WorldAudioEventsBus instance;

        public static WorldAudioEventsBus Instance
        {
            get
            {
                return instance ??= new WorldAudioEventsBus();
            }
        }

        public event Action<int, float, bool> PlayLoopingUIAudioEvent;

        public void SendPlayLandscapeAudioEvent(int index, float volume)
        {
            PlayLoopingUIAudioEvent?.Invoke(index, volume, true);
        }

        public void SendStopLandscapeAudioEvent(int index)
        {
            PlayLoopingUIAudioEvent?.Invoke(index, 0, false);
        }



        public void Dispose()
        {
        }
    }
}
