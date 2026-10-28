using ECS.Prioritization.Components;

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

        public SceneEmoteRawGltfFallback(string emotePath, string emoteHash, IPartitionComponent partition)
        {
            EmotePath = emotePath;
            EmoteHash = emoteHash;
            Partition = partition;
        }
    }
}
