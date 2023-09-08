using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using System.Linq;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearableDTO[], GetWearableDTOByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PrepareAvatarSystem : BaseUnityLoopSystem
    {
        private readonly string CATALYST_URL;

        public PrepareAvatarSystem(World world, string catalystURL) : base(world)
        {
            CATALYST_URL = catalystURL;
        }

        protected override void Update(float t)
        {
            //TODO: Release flow
            //TODO: Cancel flow
            StartAvatarLoadQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarLoad(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            //TODO: Is this OK?
            string[] wearablesToLoad = new List<string> { pbAvatarShape.BodyShape }
                                      .Concat(pbAvatarShape.Wearables)
                                      .ToArray();

            World.Add(entity, new GetWearablesByPointersIntention
                {
                    Pointers = wearablesToLoad,
                    BodyShape = pbAvatarShape,
                }, partition,
                new AvatarShapeComponent(pbAvatarShape.Id, pbAvatarShape));
        }
    }
}
