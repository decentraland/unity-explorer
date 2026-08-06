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

        /// <summary>
        ///     Routes every asset of this attachment to a raw content server, bypassing asset bundles entirely:
        ///     the manifest sentinel marks the attachment as never having an asset bundle.
        /// </summary>
        public void SetRawContentSource(string contentDownloadUrl)
        {
            ContentDownloadUrl = contentDownloadUrl;
            assetBundleManifestVersion = AssetBundleManifestVersion.CreateLSDAsset();
        }

        [Serializable]
        public abstract class TrimmedMetadataBase<TDataBase> where TDataBase : TrimmedDataBase
        {
            public abstract TDataBase AbstractData { get; }

            //urn
            public string id;
            public string rarity;
            public string name;
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
        public static string GetHash(this TrimmedAvatarAttachmentDTO DTO)
        {
            return DTO.id;
        }
    }
}
