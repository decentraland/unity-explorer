using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Shows the ghost renderer on AvatarBase while the avatar is loading. Animates RevealPosition 0→2 (reveal),
    ///     then when wearables are ready starts RevealTransition: coordinated line-up (0→2) with wearables; when done, Phase is Hidden and ghost is disabled.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarGhostSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private float deltaTime;

        internal AvatarGhostSystem(World world, IComponentPool<AvatarBase> avatarPoolRegistry) : base(world)
        {
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        protected override void Update(float t)
        {
            deltaTime = t;
            EnsureGhostAvatarQuery(World);
            UpdateGhostRevealAnimationQuery(World);
            CheckWearablesReadyStartRevealTransitionQuery(World);
            UpdateRevealTransitionAnimationQuery(World);
        }

        [Query]
        [None(typeof(AvatarBase), typeof(DeleteEntityIntention))]
        private void EnsureGhostAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar Ghost {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            if (transformComponent.Transform != null)
            {
                avatarTransform.SetParent(transformComponent.Transform, false);

                using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

                for (var index = 0; index < children.Value.Count; index++)
                {
                    Transform child = children.Value[index];

                    if (child != null)
                        child.gameObject.layer = transformComponent.Transform.gameObject.layer;
                }
            }

            avatarTransform.ResetLocalTRS();

            if (avatarBase.GhostRenderer != null)
            {
                avatarBase.GhostRenderer.SetActive(true);
                Renderer r = avatarBase.GhostRenderer.GetComponent<Renderer>();

                if (r != null && r.material != null)
                {
                    r.material.SetVector(AvatarGhostComponent.REVEAL_POSITION_SHADER_ID, new Vector4(0, AvatarGhostComponent.HIDE_TARGET, 0, 0));

                    Color nametagColor = World.TryGet(entity, out Profile? profile)
                        ? profile!.UserNameColor
                        : NameColorHelper.GetNameColor(avatarShapeComponent.Name);

                    r.material.SetColor(AvatarGhostComponent.COLOR_SHADER_ID, nametagColor);
                }
            }

            avatarBase.gameObject.SetActive(true);

            World.Add(entity, avatarBase, (IAvatarView)avatarBase, new AvatarGhostComponent(avatarBase.GhostRenderer!));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateGhostRevealAnimation(ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Revealing) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / AvatarGhostComponent.REVEAL_DURATION_SEC);
            avatarGhostComponent.RevealPosition = Mathf.Lerp(AvatarGhostComponent.HIDE_TARGET, AvatarGhostComponent.REVEAL_TARGET, progress);
            avatarGhostComponent.ApplyRevealPositionToMaterial();

            if (progress >= 1f)
            {
                avatarGhostComponent.RevealPosition = AvatarGhostComponent.REVEAL_TARGET;
                avatarGhostComponent.Phase = AvatarGhostPhase.Visible;
                avatarGhostComponent.PhaseElapsed = 0f;
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CheckWearablesReadyStartRevealTransition(ref AvatarShapeComponent avatarShapeComponent, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Visible) return;

            //if (AvatarGhostComponent.DEBUG_FREEZE_GHOST) return;

            if (!avatarShapeComponent.IsReady) return;

            avatarGhostComponent.Phase = AvatarGhostPhase.RevealTransition;
            avatarGhostComponent.PhaseElapsed = 0f;
            avatarGhostComponent.RevealPosition = AvatarGhostComponent.HIDE_TARGET;
            avatarGhostComponent.FlipRevealNormalForTransition();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateRevealTransitionAnimation(ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.RevealTransition) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / AvatarGhostComponent.HIDE_DURATION_SEC);
            avatarGhostComponent.RevealPosition = Mathf.Lerp(AvatarGhostComponent.HIDE_TARGET, AvatarGhostComponent.REVEAL_TARGET, progress);
            avatarGhostComponent.ApplyRevealPositionToMaterial();

            if (progress >= 1f)
            {
                avatarGhostComponent.RevealPosition = AvatarGhostComponent.REVEAL_TARGET;
                avatarGhostComponent.Phase = AvatarGhostPhase.Hidden;
                avatarGhostComponent.PhaseElapsed = 0f;
                avatarGhostComponent.ResetRevealNormal();
                avatarGhostComponent.Disable();
            }
        }
    }
}
