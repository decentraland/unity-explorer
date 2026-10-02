using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using ECS.Abstract;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using TMPro;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateBefore(typeof(ReleasePoolableComponentSystem<TextMeshPro, TextShapeComponent>))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    [ThrottlingEnabled]
    public partial class ReleaseTextShapeSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IFontsStorage fontsStorage;
        private readonly IComponentPool<TextMeshPro> textMeshProPool;

        internal ReleaseTextShapeSystem(World world, IFontsStorage fontsStorage, IComponentPool<TextMeshPro> textMeshProPool) : base(world)
        {
            this.fontsStorage = fontsStorage;
            this.textMeshProPool = textMeshProPool;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<TextShapeComponent>(in HandleComponentRemoval_QueryDescription);
        }

        public void FinalizeComponents(in Query query) =>
            ReleaseFontsQuery(World);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref TextShapeComponent textShapeComponent, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion) return;

            ReleaseFont(ref textShapeComponent);
        }

        [Query]
        [None(typeof(PBTextShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref TextShapeComponent textShapeComponent)
        {
            ReleaseFont(ref textShapeComponent);
            textMeshProPool.Release(textShapeComponent.TextMeshPro);
        }

        [Query]
        private void ReleaseFonts(ref TextShapeComponent textShapeComponent) =>
            ReleaseFont(ref textShapeComponent);

        private void ReleaseFont(ref TextShapeComponent textShapeComponent)
        {
            textShapeComponent.FontRequest.Release(World);

            if (textShapeComponent.CustomFont == null)
                return;

            textShapeComponent.CustomFont = null;
            TMP_FontAsset? builtInFont = fontsStorage.Font(Font.FSansSerif);

            if (builtInFont != null)
                TMPProSdkExtensions.SetFont(ref textShapeComponent, builtInFont);
        }
    }
}
