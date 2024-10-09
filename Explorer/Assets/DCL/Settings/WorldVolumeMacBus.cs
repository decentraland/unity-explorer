using System;

namespace DCL.Settings
{
    public class WorldVolumeMacBus
    {
        public event Action<float> OnWorldVolumeChanged;

        public void SetWorldVolume(float volume)
        {
            OnWorldVolumeChanged?.Invoke(volume);
        }
    }
}
