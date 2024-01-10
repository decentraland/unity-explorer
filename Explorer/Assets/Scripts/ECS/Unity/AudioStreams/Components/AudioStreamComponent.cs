using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;

namespace ECS.Unity.AudioStreams.Components
{
    public struct AudioStreamComponent
    {
        public readonly PBAudioStream PBAudioStream;
        public readonly MediaPlayer MediaPlayer;

        public AudioStreamComponent(PBAudioStream pbAudioStream, MediaPlayer mediaPlayer)
        {
            PBAudioStream = pbAudioStream;
            MediaPlayer = mediaPlayer;
        }
    }
}
