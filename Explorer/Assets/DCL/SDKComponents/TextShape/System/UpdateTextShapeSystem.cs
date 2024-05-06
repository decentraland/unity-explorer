using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.Unity.Groups;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateTextShapeSystem))]
    public partial class UpdateTextShapeSystem : BaseUnityLoopSystem
    {
        private readonly IFontsStorage fontsStorage;
        private readonly MaterialPropertyBlock materialPropertyBlock;

        public UpdateTextShapeSystem(World world, IFontsStorage fontsStorage, MaterialPropertyBlock materialPropertyBlock) : base(world)
        {
            this.fontsStorage = fontsStorage;
            this.materialPropertyBlock = materialPropertyBlock;
        }

        protected override void Update(float t)
        {
            UpdateTextsQuery(World!);
        }

        [Query]
        private void UpdateTexts(in TextShapeRendererComponent textShapeComponent, in PBTextShape textShape)
        {
            if (textShape.IsDirty)
            {
                textShapeComponent.TextMeshPro.Apply(textShape, fontsStorage, materialPropertyBlock);
                textShape.IsDirty = false;
            }
        }
    }
}
