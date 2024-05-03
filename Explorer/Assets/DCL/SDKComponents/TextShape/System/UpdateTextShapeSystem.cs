using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Systems;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTextShapeSystem))]
    [UpdateBefore(typeof(ParentingTransformSystem))]
    public partial class UpdateTextShapeSystem : BaseUnityLoopSystem
    {
        public UpdateTextShapeSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateTextsQuery(World!);
        }

        [Query]
        private void UpdateTexts(in TextShapeRendererComponent textShapeRenderer, in PBTextShape textShape)
        {
            if (textShape.IsDirty)
            {
                textShapeRenderer.Apply(textShape);
                textShape.IsDirty = false;
            }
        }
    }
}
