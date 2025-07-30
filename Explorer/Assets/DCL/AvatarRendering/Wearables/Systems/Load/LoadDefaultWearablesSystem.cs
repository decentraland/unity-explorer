using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(InitializationSystemGroup))] // It is updated first so other systems can depend on it asap
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        private readonly IWearableStorage wearableStorage;
        private readonly GameObject emptyDefaultWearable;

        internal LoadDefaultWearablesSystem(World world,
            GameObject emptyDefaultWearable,
            IWearableStorage wearableStorage) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.emptyDefaultWearable = emptyDefaultWearable;
        }

        public override void Initialize()
        {
            // Update this data if by any reason is changed in the server
            AddEmptyWearable();
            AddBodyShapes();
        }

        protected override void Update(float t)
        {
            TryConsumeDefaultWearablesPromiseQuery(World);
        }

        [Query]
        private void TryConsumeDefaultWearablesPromise(ref DefaultWearablesComponent defaultWearablesComponent)
        {
            if (defaultWearablesComponent.ResolvedState != DefaultWearablesComponent.State.InProgress)
                return;

            var allPromisesAreConsumed = true;
            DefaultWearablesComponent.State finalState = DefaultWearablesComponent.State.Success;

            for (var i = 0; i < defaultWearablesComponent.PromisePerBodyShape.Length; i++)
            {
                ref AssetPromise<WearablesResolution, GetWearablesByPointersIntention> promise = ref defaultWearablesComponent.PromisePerBodyShape[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<WearablesResolution> result))
                {
                    if (!result.Succeeded)
                        finalState = DefaultWearablesComponent.State.Fail;
                }
                else allPromisesAreConsumed = false;
            }

            if (allPromisesAreConsumed)
                defaultWearablesComponent.ResolvedState = finalState;
        }

        private void AddBodyShapes()
        {
            var state = new DefaultWearablesComponent(new AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[BodyShape.COUNT]);

            {
                WearableDTO dto = new WearableDTO
                {
                    id = "bafkreia6mv65u2cq7poxmaxoxgm3cyuazlgrbhg5pkgi4kzsmrlgdxqcgq",
                    version = "v3",
                    type = "wearable",
                    pointers = new[] { "urn:decentraland:off-chain:base-avatars:basemale" },
                    timestamp = 1689946443828,
                    content = new[]
                    {
                        new AvatarAttachmentDTO.Content { file = "Avatar_MaleSkinBase.png", hash = "bafkreiaubk2exzcqiutttnjajktvu6uys3zeb4poxpzauv6yccghz5heta" },
                        new AvatarAttachmentDTO.Content { file = "BaseMale.glb", hash = "bafkreicxwj4mhufwziveusb733zs3ly2vz7evnpsbybto3izqtfrbr7umq" },
                        new AvatarAttachmentDTO.Content { file = "M_EyeBrows_00.png", hash = "bafkreiax475w7ueo4idkzy4i2mizzmfqynipyzb2v4kp5bqncujlqnhxky" },
                        new AvatarAttachmentDTO.Content { file = "M_Eyes_00.png", hash = "bafkreihse7i7mqabinjq4aydv3kngpxkoownwx2z3ul5ko3vz3zcn6qzii" },
                        new AvatarAttachmentDTO.Content { file = "M_Mouth_00.png", hash = "bafkreih3id67pbqkfw2ikggwv6uz5dmroo4z2nkcdjbzbkpzjhd3rfbz6y" },
                        new AvatarAttachmentDTO.Content { file = "thumbnail.png", hash = "bafkreihqzejw6hhse4tupftqaaxnnwvobslj4xdwwpaj2rbqj4n2o2ymyy" },
                    },
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        id = "urn:decentraland:off-chain:base-avatars:BaseMale",
                        name = "BaseMale",
                        description = string.Empty,
                        thumbnail = "thumbnail.png",
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            replaces = Array.Empty<string>(),
                            hides = Array.Empty<string>(),
                            tags = new[] { "body", "male", "man", "base-wearable" },
                            category = WearablesConstants.Categories.BODY_SHAPE,
                            representations = new[]
                            {
                                new AvatarAttachmentDTO.Representation
                                {
                                    bodyShapes = new[] { "urn:decentraland:off-chain:base-avatars:BaseMale" },
                                    mainFile = "BaseMale.glb",
                                    overrideReplaces = Array.Empty<string>(),
                                    overrideHides = Array.Empty<string>(),
                                    contents = new[]
                                    {
                                        "Avatar_MaleSkinBase.png",
                                        "BaseMale.glb",
                                        "M_EyeBrows_00.png",
                                        "M_Eyes_00.png",
                                        "M_Mouth_00.png",
                                    }
                                }
                            },
                        },
                        i18n = new[]
                        {
                            new AvatarAttachmentDTO.I18n
                            {
                                code = "en",
                                text = "Man",
                            },
                            new AvatarAttachmentDTO.I18n
                            {
                                code = "es",
                                text = "Hombre",
                            },
                        },
                    },
                };

                IWearable wearable = wearableStorage.GetOrAddByDTO(dto, false);

                state.PromisePerBodyShape[BodyShape.MALE] = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>
                   .Create(World, new GetWearablesByPointersIntention(new List<URN>{wearable.GetUrn()}, BodyShape.MALE,
                        Array.Empty<string>(), AssetSource.EMBEDDED),
                        PartitionComponent.TOP_PRIORITY);
            }

            {
                WearableDTO dto = new WearableDTO
                {
                    id = "bafkreiex32wmcz7t7owodykgvy57xj57upnzkx7pjmfksxd7ebsqyyzjla",
                    version = "v3",
                    type = "wearable",
                    pointers = new[] { "urn:decentraland:off-chain:base-avatars:basefemale" },
                    timestamp = 1689946443741,
                    content = new[]
                    {
                        new AvatarAttachmentDTO.Content { file = "Avatar_FemaleSkinBase.png", hash = "bafkreidgli7y7lyskioyjcgkub3ja2af7b2cj7hsjjjqvgifjk7eusixoe" },
                        new AvatarAttachmentDTO.Content { file = "BaseFemale.glb", hash = "bafkreicjhpml7xdib2knhbl2qn7sgqq7cudwys75xwgejdd47cxp3dkbc4" },
                        new AvatarAttachmentDTO.Content { file = "F_Eyebrows_00.png", hash = "bafkreicljlsrh7upl5guvinmtjjqn7eagyu7e6wsef4a5nyerjuyw7t5fu" },
                        new AvatarAttachmentDTO.Content { file = "F_Eyes_00.png", hash = "bafkreihm3s5xcauc6i256xnywwssnodcvtrs6z3454itsf2ph63e3tx7iq" },
                        new AvatarAttachmentDTO.Content { file = "F_Mouth_00.png", hash = "bafkreiaryit63vshyvyddoo3dfjdapvlfcyf2jfbd6enktal3kbv2pcdru" },
                        new AvatarAttachmentDTO.Content { file = "thumbnail.png", hash = "bafkreigohcob7ium7ynqeya6ceavkkuvdndx6kjprgqah4lgpvmze6jzji" },
                    },
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        id = "urn:decentraland:off-chain:base-avatars:BaseFemale",
                        name = "BaseFemale",
                        description = string.Empty,
                        thumbnail = "thumbnail.png",
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            replaces = Array.Empty<string>(),
                            hides = Array.Empty<string>(),
                            tags = new[] { "body", "female", "woman", "base-wearable" },
                            category = WearablesConstants.Categories.BODY_SHAPE,
                            representations = new[]
                            {
                                new AvatarAttachmentDTO.Representation
                                {
                                    bodyShapes = new[] { "urn:decentraland:off-chain:base-avatars:BaseFemale" },
                                    mainFile = "BaseFemale.glb",
                                    overrideReplaces = Array.Empty<string>(),
                                    overrideHides = Array.Empty<string>(),
                                    contents = new[]
                                    {
                                        "Avatar_FemaleSkinBase.png",
                                        "BaseFemale.glb",
                                        "F_Eyebrows_00.png",
                                        "F_Eyes_00.png",
                                        "F_Mouth_00.png",
                                    }
                                }
                            },
                        },
                        i18n = new[]
                        {
                            new AvatarAttachmentDTO.I18n
                            {
                                code = "en",
                                text = "Woman",
                            },
                            new AvatarAttachmentDTO.I18n
                            {
                                code = "es",
                                text = "Mujer",
                            },
                        },
                    },
                };

                IWearable wearable = wearableStorage.GetOrAddByDTO(dto, false);

                state.PromisePerBodyShape[BodyShape.FEMALE] = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>
                   .Create(World, new GetWearablesByPointersIntention(new List<URN>{wearable.GetUrn()}, BodyShape.FEMALE,
                        Array.Empty<string>(), AssetSource.EMBEDDED),
                        PartitionComponent.TOP_PRIORITY);
            }

            World.Create(state);
        }

        private void AddEmptyWearable()
        {
            var wearableDTO = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = WearablesConstants.EMPTY_DEFAULT_WEARABLE,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = WearablesConstants.Categories.HELMET
                    }
                },
            };

            var mesh = new Mesh();

            mesh.vertices = new[]
            {
                Vector3.zero
            };

            var boneWeights = new BoneWeight[1];
            boneWeights[0].weight0 = 1; // 100% influence from the first (and only) bone
            mesh.boneWeights = boneWeights;

            var rendererInfos = new List<AttachmentRegularAsset.RendererInfo>();

            foreach (var skinnedMeshRenderer in emptyDefaultWearable.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.sharedMesh = mesh;
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer.sharedMaterial));
            }

            IWearable emptyWearable = wearableStorage.GetOrAddByDTO(wearableDTO, false);
            var wearableAsset = new AttachmentRegularAsset(emptyDefaultWearable, rendererInfos, null);
            wearableAsset.AddReference();

            // only game-objects here
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.MALE);
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.FEMALE);
        }
    }
}
