using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenLoaderSystem : BaseUnityLoopSystem
    {
        public TweenLoaderSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadTweenQuery(World);
        }

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTween)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            var sdkTweenComponent = new SDKTweenComponent
            {
                IsDirty = true,
            };

            if (pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove)
            {
                var sdkTweenTextureComponent = new SDKTweenTextureComponent(pbTween.TextureMove.MovementType);

                World.Add(entity, sdkTweenComponent, sdkTweenTextureComponent);
            }
            else { World.Add(entity, sdkTweenComponent); }
        }
    }
}
