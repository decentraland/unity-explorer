using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.Unity.Groups;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateTextShapeSystem))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class UpdateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly IFontsStorage fontsStorage;
        private readonly MaterialPropertyBlock materialPropertyBlock;

        private readonly EntityEventBuffer<TextShapeComponent> changedTextMeshes;

        public UpdateTextShapeSystem(World world, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock,
            EntityEventBuffer<TextShapeComponent> changedTextMeshes) : base(world)
        {
            this.fontsStorage = fontsStorage;
            this.materialPropertyBlock = materialPropertyBlock;
            this.changedTextMeshes = changedTextMeshes;
        }

        protected override void Update(float t)
        {
            UpdateTextsQuery(World!);
        }

        [Query]
        private void UpdateTexts(Entity entity, in TextShapeComponent textShapeComponent, in PBTextShape textShape)
        {
            if (textShape.IsDirty)
            {
                textShapeComponent.TextMeshPro.Apply(textShape, fontsStorage, materialPropertyBlock);
                changedTextMeshes.Add(entity, textShapeComponent);
                textShape.IsDirty = false;
            }
        }
    }
}
