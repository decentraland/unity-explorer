using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class CreateVideoTextureSystem: BaseUnityLoopSystem
    {
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        private CreateVideoTextureSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap) : base(world)
        {
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
           ResolveVideoTextureComponentQuery(World);
        }


        [Query]
        private void ResolveVideoTextureComponent(ref GetTextureIntention intention)
        {

            // if (intention.IsVideoTexture)
            // {
            //     Debug.Log(intention);
            // }
        }
    }
}
