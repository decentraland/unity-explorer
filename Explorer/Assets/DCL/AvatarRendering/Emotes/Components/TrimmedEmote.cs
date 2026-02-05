using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Emotes
{
    public class TrimmedEmote : ITrimmedEmote
    {
        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public int Amount { get; set; }
        public TrimmedEmoteDTO TrimmedDTO => TrimmedModel.Asset!;
        public StreamableLoadingResult<TrimmedEmoteDTO> TrimmedModel { get; set; }

        public TrimmedEmote(TrimmedEmoteDTO model)
        {
            TrimmedModel = new StreamableLoadingResult<TrimmedEmoteDTO>(model);
        }
    }
}
