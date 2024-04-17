using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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

        public event Action<int, NativeArray<int2>, WorldAudioClipType> PlayWorldAudioEvent;
        public event Action<int, WorldAudioClipType> StopWorldAudioEvent;


        public void SendPlayLandscapeAudioEvent(int index, NativeArray<int2> audioSourcesPositions, WorldAudioClipType clipType)
        {
            PlayWorldAudioEvent?.Invoke(index, audioSourcesPositions, clipType);
        }

        public void SendStopLandscapeAudioEvent(int index, WorldAudioClipType clipType)
        {
            StopWorldAudioEvent?.Invoke(index, clipType);
        }



        public void Dispose()
        {
        }
    }
}
