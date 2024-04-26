using System;
using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Audio
{
    public class WorldAudioEventsBus
    {
        private static WorldAudioEventsBus instance;

        public static WorldAudioEventsBus Instance
        {
            get
            {
                return instance ??= new WorldAudioEventsBus();
            }
        }

        public event Action<int, NativeArray<int2>, WorldAudioClipType> PlayLandscapeAudioEvent;
        public event Action<int, WorldAudioClipType> StopWorldAudioEvent;

        public void SendPlayTerrainAudioEvent(int index, NativeArray<int2> audioSourcesPositions, WorldAudioClipType clipType)
        {
            PlayLandscapeAudioEvent?.Invoke(index, audioSourcesPositions, clipType);
        }

        public void SendStopTerrainAudioEvent(int index, WorldAudioClipType clipType)
        {
            StopWorldAudioEvent?.Invoke(index, clipType);
        }
    }
}
