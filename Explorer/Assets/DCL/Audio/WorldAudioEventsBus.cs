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

        public event Action<int, NativeArray<int2>> PlayLandscapeAudioEvent;
        public event Action<int> StopLandscapeAudioEvent;


        public void SendPlayLandscapeAudioEvent(int index, NativeArray<int2> audioSourcesPositions)
        {
            PlayLandscapeAudioEvent?.Invoke(index, audioSourcesPositions);
        }

        public void SendStopLandscapeAudioEvent(int index)
        {
            StopLandscapeAudioEvent?.Invoke(index);
        }



        public void Dispose()
        {
        }
    }
}
