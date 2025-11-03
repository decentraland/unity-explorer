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

        TrimmedAvatarAttachmentDTO DTO { get; }

        public string ToString() =>
            $"TrimmedAvatarAttachment({DTO.GetHash()} | {this.GetUrn()})";
    }

    public interface ITrimmedAvatarAttachment<TModelDTO> : ITrimmedAvatarAttachment
    {
        StreamableLoadingResult<TModelDTO> Model { get; set; }

        bool IsOnChain();

        public void ResolvedFailedDTO(StreamableLoadingResult<TModelDTO> result)
        {
            Model = result;
            UpdateLoadingStatus(false);
        }

        public void ApplyAndMarkAsLoaded(TModelDTO modelDTO)
        {
            Model = new StreamableLoadingResult<TModelDTO>(modelDTO);
            UpdateLoadingStatus(false);
        }
    }

    public static class TrimmedAvatarAttachmentExtensions
    {
        public static URN GetUrn(this ITrimmedAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.id;

        public static string GetRarity(this ITrimmedAvatarAttachment avatarAttachment)
        {
            const string DEFAULT_RARITY = "base";
            string result = avatarAttachment.DTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        public static string GetCategory(this ITrimmedAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.AbstractData.category;
    }
}
