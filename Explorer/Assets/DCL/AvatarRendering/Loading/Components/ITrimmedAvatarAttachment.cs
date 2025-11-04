using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface ITrimmedAvatarAttachment
    {
        bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading);

        /// <summary>
        ///     If null - promise has never been created, otherwise it could contain the result or be un-initialized
        /// </summary>
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        TrimmedAvatarAttachmentDTO TrimmedDTO { get; }

        public string ToString() =>
            $"TrimmedAvatarAttachment({TrimmedDTO.GetHash()} | {this.GetUrn()})";
    }

    public interface ITrimmedAvatarAttachment<TModelDTO> : ITrimmedAvatarAttachment
    {
        StreamableLoadingResult<TModelDTO> TrimmedModel { get; set; }

        bool IsOnChain();

        public void ResolvedFailedDTO(StreamableLoadingResult<TModelDTO> result)
        {
            TrimmedModel = result;
            UpdateLoadingStatus(false);
        }

        public void ApplyAndMarkAsLoaded(TModelDTO modelDTO)
        {
            TrimmedModel = new StreamableLoadingResult<TModelDTO>(modelDTO);
            UpdateLoadingStatus(false);
        }
    }

    public static class TrimmedAvatarAttachmentExtensions
    {
        public static URN GetUrn(this ITrimmedAvatarAttachment avatarAttachment) =>
            avatarAttachment.TrimmedDTO.Metadata.id;

        public static string GetRarity(this ITrimmedAvatarAttachment avatarAttachment)
        {
            const string DEFAULT_RARITY = "base";
            string result = avatarAttachment.TrimmedDTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        public static string GetCategory(this ITrimmedAvatarAttachment avatarAttachment) =>
            avatarAttachment.TrimmedDTO.Metadata.AbstractData.category;
    }
}
