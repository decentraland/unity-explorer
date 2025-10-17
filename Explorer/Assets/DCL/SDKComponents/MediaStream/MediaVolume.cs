using DCL.Audio;

namespace DCL.SDKComponents.MediaStream
{
    public class MediaVolume
    {
        private readonly VolumeBus volumeBus;

        public MediaVolume(VolumeBus volumeBus)
        {
            //This following part is a workaround applied for the MacOS platform, the reason
            //is related to the video and audio streams, the MacOS environment does not support
            //the volume control for the video and audio streams, as it doesn’t allow to route audio
            //from HLS through to Unity. This is a limitation of Apple’s AVFoundation framework
            //Similar issue reported here https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1086
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            this.volumeBus = volumeBus;
            this.volumeBus.OnMasterVolumeChanged += OnMasterVolumeChanged;
            this.volumeBus.OnWorldVolumeChanged += OnWorldVolumeChanged;
            masterVolumePercentage = volumeBus.GetSerializedMasterVolume();
            worldVolumePercentage = volumeBus.GetSerializedWorldVolume();

            void OnWorldVolumeChanged(float volume) => worldVolumePercentage = volume;
            void OnMasterVolumeChanged(float volume) => masterVolumePercentage = volume;
#endif
        }

        public float WorldVolumePercentage { get; private set; } = 1f;

        public float MasterVolumePercentage { get; private set; } = 1f;

        public void Dispose()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.OnWorldVolumeChanged -= OnWorldVolumeChanged;
            volumeBus.OnMasterVolumeChanged -= OnMasterVolumeChanged;
#endif
        }
    }
}
