using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Ipfs;
using Runtime.Wearables;
using SceneRunner.Scene;
using System;
using System.Diagnostics.CodeAnalysis;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class SceneWearableDTOFactory
    {
        public const string SCENE_WEARABLE_PREFIX = "urn:decentraland:off-chain:scene-wearable";

        private const string SCENE_WEARABLE_URN_START = SCENE_WEARABLE_PREFIX + ":";
        private const string GLB_EXTENSION = ".glb";
        private const string WEARABLE_ENTITY_TYPE = "wearable";
        private const string ENTITY_VERSION = "v3";

        private static readonly string[] GLB_CATEGORIES =
        {
            WearableCategories.Categories.UPPER_BODY,
            WearableCategories.Categories.LOWER_BODY,
            WearableCategories.Categories.FEET,
            WearableCategories.Categories.HANDS_WEAR,
            WearableCategories.Categories.HAT,
            WearableCategories.Categories.HELMET,
            WearableCategories.Categories.TOP_HEAD,
            WearableCategories.Categories.MASK,
            WearableCategories.Categories.EYEWEAR,
            WearableCategories.Categories.EARRING,
            WearableCategories.Categories.TIARA,
            WearableCategories.Categories.HAIR,
            WearableCategories.Categories.FACIAL_HAIR,
            WearableCategories.Categories.SKIN,
        };

        public static bool IsSceneWearablePath(string pointer) =>
            pointer.EndsWith(GLB_EXTENSION, StringComparison.OrdinalIgnoreCase);

        public static bool IsSceneWearableUrn(string pointer) =>
            pointer.StartsWith(SCENE_WEARABLE_URN_START, StringComparison.OrdinalIgnoreCase);

        public static string GetUrnPrefix(string? sceneId) =>
            $"{SCENE_WEARABLE_URN_START}{sceneId}-";

        /// <summary>
        ///     Picks the longest matching category, so <c>beard_facial_hair.glb</c> resolves to <c>facial_hair</c>, not <c>hair</c>.
        /// </summary>
        public static bool TryGetCategory(string path, [NotNullWhen(true)] out string? category)
        {
            category = null;

            if (!IsSceneWearablePath(path))
                return false;

            int fileNameStart = path.LastIndexOf('/') + 1;
            ReadOnlySpan<char> stem = path.AsSpan(fileNameStart, path.Length - fileNameStart - GLB_EXTENSION.Length);

            foreach (string candidate in GLB_CATEGORIES)
            {
                int separatorIndex = stem.Length - candidate.Length - 1;

                if (separatorIndex < 0 || stem[separatorIndex] != '_') continue;
                if (!stem.EndsWith(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase)) continue;

                if (category == null || candidate.Length > category.Length)
                    category = candidate;
            }

            return category != null;
        }

        public static bool TryGetUrn(ISceneData sceneData, string path, bool local, out URN urn,
            [NotNullWhen(true)] out string? category, [NotNullWhen(true)] out string? hash)
        {
            urn = default;
            hash = null;

            var reportData = new ReportData(ReportCategory.WEARABLE, sceneShortInfo: sceneData.SceneShortInfo);

            if (!TryGetCategory(path, out category))
            {
                ReportHub.LogWarning(reportData, $"Scene wearable '{path}' skipped: the file name must end with '_<category>.glb' using a supported category");
                return false;
            }

            if (!sceneData.SceneContent.TryGetHash(path, out hash))
            {
                ReportHub.LogWarning(reportData, $"Scene wearable '{path}' skipped: the file is not part of the scene content");
                return false;
            }

            SceneEntityDefinition definition = sceneData.SceneEntityDefinition;

            if (string.IsNullOrEmpty(definition.id))
            {
                ReportHub.LogWarning(reportData, $"Scene wearable '{path}' skipped: the scene has no id");
                return false;
            }

            if (!local && definition.assetBundleManifestVersion is null or { assetBundleManifestRequestFailed: true })
            {
                ReportHub.LogWarning(reportData, $"Scene wearable '{path}' skipped: the scene has no asset bundle manifest");
                return false;
            }

            urn = new URN($"{GetUrnPrefix(definition.id)}{hash}");
            return true;
        }

        public static WearableDTO Create(ISceneData sceneData, string path, URN urn, string category, string hash, bool local)
        {
            SceneEntityDefinition definition = sceneData.SceneEntityDefinition;

            WearableDTO dto = local ? new LocalSceneWearableDTO(sceneData.SceneContent.ContentBaseUrl.Value) : new WearableDTO();
            dto.id = definition.id;
            dto.type = WEARABLE_ENTITY_TYPE;
            dto.version = ENTITY_VERSION;
            dto.pointers = new string[] { urn };
            dto.content = CollectFolderContent(definition.content, path, hash);
            dto.assetBundleManifestVersion = definition.assetBundleManifestVersion;

            dto.metadata = new WearableDTO.WearableMetadataDto
            {
                id = urn,
                name = path.Substring(path.LastIndexOf('/') + 1),
                description = string.Empty,
                thumbnail = string.Empty,
                i18n = Array.Empty<AvatarAttachmentDTO.I18n>(),
                data = new WearableDTO.WearableMetadataDto.DataDto
                {
                    category = category,
                    hides = Array.Empty<string>(),
                    replaces = Array.Empty<string>(),
                    removesDefaultHiding = new[] { WearableCategories.Categories.HANDS },
                    tags = Array.Empty<string>(),
                    representations = new[]
                    {
                        new AvatarAttachmentDTO.Representation
                        {
                            bodyShapes = new[] { BodyShape.MALE.Value, BodyShape.FEMALE.Value },
                            mainFile = path,
                            contents = new[] { path },
                            overrideHides = Array.Empty<string>(),
                            overrideReplaces = Array.Empty<string>(),
                        },
                    },
                },
            };

            return dto;
        }

        private static ContentDefinition[] CollectFolderContent(ContentDefinition[] sceneContent, string mainPath, string mainHash)
        {
            int folderLength = mainPath.LastIndexOf('/') + 1;
            var siblingsCount = 0;

            foreach (ContentDefinition file in sceneContent)
                if (IsSibling(file.file, mainPath, folderLength))
                    siblingsCount++;

            var result = new ContentDefinition[siblingsCount + 1];
            result[0] = new ContentDefinition { file = mainPath, hash = mainHash };

            var index = 1;

            foreach (ContentDefinition file in sceneContent)
                if (IsSibling(file.file, mainPath, folderLength))
                    result[index++] = file;

            return result;
        }

        private static bool IsSibling(string file, string mainPath, int folderLength) =>
            file.Length > folderLength
            && string.Compare(file, 0, mainPath, 0, folderLength, StringComparison.OrdinalIgnoreCase) == 0
            && file.IndexOf('/', folderLength) < 0
            && !string.Equals(file, mainPath, StringComparison.OrdinalIgnoreCase);

        private sealed class LocalSceneWearableDTO : WearableDTO
        {
            public LocalSceneWearableDTO(string contentDownloadUrl)
            {
                ContentDownloadUrl = contentDownloadUrl;
            }
        }
    }
}
