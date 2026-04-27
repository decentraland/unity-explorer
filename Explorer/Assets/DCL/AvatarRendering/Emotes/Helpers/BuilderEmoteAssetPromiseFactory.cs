using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.SDKComponents.AudioSources;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.GLTF;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public static class BuilderEmoteAssetPromiseFactory
    {
        private static readonly BodyShape[] ALL_BODYSHAPES = { BodyShape.MALE, BodyShape.FEMALE };

        public static bool TryCreate(World world, IEmote emote, IPartitionComponent partition, IEmoteStorage emoteStorage, IURLBuilder urlBuilder)
        {
            if (string.IsNullOrEmpty(emote.DTO?.ContentDownloadUrl))
                return false;

            if (emote.IsLoading)
                return true;

            if (emoteStorage.TryGetElement(emote.GetUrn(), out IEmote existingEmote))
            {
                if (existingEmote.IsUnisex() && existingEmote.HasSameClipForAllGenders())
                {
                    if (existingEmote.AssetResults[BodyShape.MALE] != null || existingEmote.AssetResults[BodyShape.FEMALE] != null)
                        return false;
                }
                else
                {
                    if (existingEmote.AssetResults[BodyShape.MALE] != null && existingEmote.AssetResults[BodyShape.FEMALE] != null)
                        return false;
                }
            }

            bool foundGlb = false;
            bool stillProcessing = false;

            BodyShape? targetBodyShape = null;
            if (!emote.IsUnisex())
                targetBodyShape = BodyShape.FromStringSafe(emote.DTO.Metadata.AbstractData.representations[0].bodyShapes[0]);

            foreach (var content in emote.DTO.content)
            {
                if (content.file.EndsWith(".glb"))
                {
                    for (int i = 0; i < ALL_BODYSHAPES.Length; i++)
                    {
                        BodyShape bodyShape = ALL_BODYSHAPES[i];
                        if (!emote.IsUnisex() && !bodyShape.Equals(targetBodyShape!))
                            continue;

                        if (emote.AssetResults[bodyShape] != null)
                            continue;

                        var gltfPromise = GltfPromise.Create(world, GetGLTFIntention.Create(content.file, content.hash), partition);
                        world.Create(gltfPromise, emote, bodyShape);
                        emote.UpdateLoadingStatus(true);
                        foundGlb = true;
                        stillProcessing = true;
                    }
                    continue;
                }

                if (content.file.EndsWith(".mp3") || content.file.EndsWith(".ogg"))
                {
                    var audioType = content.file.ToAudioType();
                    urlBuilder.Clear();
                    urlBuilder.AppendDomain(URLDomain.FromString(emote.DTO.ContentDownloadUrl)).AppendPath(new URLPath(content.hash));
                    URLAddress url = urlBuilder.Build();

                    for (int i = 0; i < ALL_BODYSHAPES.Length; i++)
                    {
                        BodyShape bodyShape = ALL_BODYSHAPES[i];
                        if (!emote.IsUnisex() && !bodyShape.Equals(targetBodyShape))
                            continue;

                        var audioPromise = AudioUtils.CreateAudioClipPromise(world, url.Value, audioType, partition);
                        world.Create(audioPromise, emote, bodyShape);
                    }

                    if (foundGlb) break;
                }
            }

            return stillProcessing;
        }
    }
}
