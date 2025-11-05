using DCL.Ipfs;
using System;

namespace DCL.AvatarRendering.Loading.DTO
{
    public abstract class TrimmedAvatarAttachmentDTO<TMetadata> : TrimmedAvatarAttachmentDTO where TMetadata : TrimmedAvatarAttachmentDTO.TrimmedMetadataBase<TrimmedAvatarAttachmentDTO.TrimmedDataBase>
    {
        public TMetadata metadata;

        public override TrimmedMetadataBase<TrimmedDataBase> Metadata => metadata;
    }

    public abstract class TrimmedAvatarAttachmentDTO : TrimmedEntityDefinitionBase
    {
        public string? ContentDownloadUrl { get; protected set; }

        public abstract TrimmedMetadataBase<TrimmedDataBase> Metadata { get; }

        [Serializable]
        public abstract class TrimmedMetadataBase<TDataBase> where TDataBase : TrimmedDataBase
        {
            public abstract TDataBase AbstractData { get; }

            //urn
            public string id;
            public string rarity;
        }

        [Serializable]
        public abstract class TrimmedDataBase
        {
            public AvatarAttachmentDTO.Representation[] representations;
            public string category;
        }
    }

    public static class TrimmedAvatarAttachmentDTOExtensions
    {
        public static string GetHash(this TrimmedAvatarAttachmentDTO DTO) =>
            DTO.id;
    }
}
