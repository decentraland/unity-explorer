using ECS.Prioritization.Components;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Sits next to a local scene emote's asset bundle promise. When that bundle fails, the emote is loaded from
    ///     the raw GLB described here instead of being marked as failed.
    /// </summary>
    public readonly struct SceneEmoteRawGltfFallback
    {
        public readonly string EmotePath;
        public readonly string EmoteHash;
        public readonly IPartitionComponent Partition;
        public readonly CancellationTokenSource CancellationTokenSource;

        public SceneEmoteRawGltfFallback(string emotePath, string emoteHash, IPartitionComponent partition, CancellationTokenSource cancellationTokenSource)
        {
            EmotePath = emotePath;
            EmoteHash = emoteHash;
            Partition = partition;
            CancellationTokenSource = cancellationTokenSource;
        }
    }
}
