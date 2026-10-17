using DCL.Audio;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaVolume
    {
        private readonly VolumeBus volumeBus;

        public float WorldVolumePercentage { get; private set; }

        public float MasterVolumePercentage { get; private set; }

        public MediaVolume(VolumeBus volumeBus)
        {
            this.volumeBus = volumeBus;
            this.volumeBus.OnMasterVolumeChanged += OnMasterVolumeChanged;
            this.volumeBus.OnWorldVolumeChanged += OnWorldVolumeChanged;
            MasterVolumePercentage = volumeBus.GetSerializedMasterVolume();
            WorldVolumePercentage = volumeBus.GetSerializedWorldVolume();
        }

        public void Dispose()
        {
            volumeBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            volumeBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
        }

        private void OnWorldVolumeChanged(float volume) => WorldVolumePercentage = volume;

        private void OnMasterVolumeChanged(float volume) => MasterVolumePercentage = volume;
    }
}
