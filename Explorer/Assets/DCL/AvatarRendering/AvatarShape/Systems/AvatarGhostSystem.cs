using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Shows the ghost renderer on AvatarBase while the avatar is loading. Animates RevealPosition 0→2 (reveal),
    ///     then when wearables are ready starts RevealTransition: coordinated line-up (0→2) with wearables; when done, Phase is Hidden and ghost is disabled.
    ///     Skips preview (backpack/passport) and SDK avatars entirely.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarGhostSystem : BaseUnityLoopSystem
    {
        private static readonly int REVEAL_POSITION_SHADER_ID = Shader.PropertyToID("_RevealPosition");
        private static readonly int REVEAL_ENABLED_SHADER_ID = Shader.PropertyToID("_RevealEnabled");
        private static readonly int COLOR_SHADER_ID = Shader.PropertyToID("_FresnelColor");
        private static readonly int REVEAL_NORMAL_SHADER_ID = Shader.PropertyToID("_RevealNormal");
        private static readonly Vector4 REVEAL_NORMAL_DEFAULT = new (0, 1, 0, 0);
        private static readonly Vector4 REVEAL_NORMAL_FLIPPED = new (0, -1, 0, 0);
        private const float REVEAL_TARGET = 3f;
        private const float HIDE_TARGET = -0.05f;

        // Time to reveal the ghost
        public const float REVEAL_DURATION_SEC = 1f;

        // Time transitioning from ghost to avatar
        public const float HIDE_DURATION_SEC = 1f;

        internal AvatarGhostSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            EnsureGhostAvatarQuery(World);
            HideNewlyInstantiatedWearablesQuery(World);
            CheckWearablesReadyStartRevealTransitionQuery(World);
            UpdateGhostRevealAnimationQuery(World, t);
            UpdateRevealTransitionAnimationQuery(World, t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(AvatarGhostComponent))]
        private void EnsureGhostAvatar(in Entity entity, ref AvatarBase avatarBase, Profile profile)
        {
            foreach (Renderer renderer in avatarBase.GhostRenderers)
            {
                renderer.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, HIDE_TARGET, 0, 0));
                renderer.material.SetColor(COLOR_SHADER_ID, profile!.UserNameColor);
                renderer.material.SetVector(REVEAL_NORMAL_SHADER_ID, REVEAL_NORMAL_DEFAULT);
            }

            avatarBase.GhostGameObject.SetActive(true);

            World.Add(entity, new AvatarGhostComponent(avatarBase.GhostRenderers));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HideNewlyInstantiatedWearables(ref AvatarShapeComponent avatarShapeComponent, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.WearablesHidden) return;
            if (avatarShapeComponent.InstantiatedWearables.Count == 0) return;

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.material == null) continue;
                    renderer.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, HIDE_TARGET, 0, 0));
                    renderer.material.SetFloat(REVEAL_ENABLED_SHADER_ID, 1f);
                }
            }

            avatarGhostComponent.WearablesHidden = true;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CheckWearablesReadyStartRevealTransition(ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Visible) return;
            if (!avatarGhostComponent.WearablesHidden) return;

            foreach (Renderer renderer in avatarGhostComponent.GhostRenderers)
                renderer.material.SetVector(REVEAL_NORMAL_SHADER_ID, REVEAL_NORMAL_FLIPPED);
            avatarGhostComponent.Phase = AvatarGhostPhase.RevealTransition;
            avatarGhostComponent.PhaseElapsed = 0f;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateGhostRevealAnimation([Data] float deltaTime, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Revealing) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / REVEAL_DURATION_SEC);
            float revealPosition = Mathf.Lerp(HIDE_TARGET, REVEAL_TARGET, progress);

            foreach (Renderer renderer in avatarGhostComponent.GhostRenderers)
                renderer.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, revealPosition, 0, 0));

            if (progress >= 1f)
            {
                avatarGhostComponent.Phase = AvatarGhostPhase.Visible;
                avatarGhostComponent.PhaseElapsed = 0f;
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateRevealTransitionAnimation([Data] float deltaTime, ref AvatarGhostComponent avatarGhostComponent, ref AvatarShapeComponent avatarShapeComponent, ref AvatarBase avatarBase)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.RevealTransition) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / HIDE_DURATION_SEC);
            float revealPosition = Mathf.Lerp(HIDE_TARGET, REVEAL_TARGET, progress);

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.material == null) continue;
                    renderer.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, revealPosition, 0, 0));
                }
            }

            foreach (Renderer ghostRenderer in avatarGhostComponent.GhostRenderers)
                ghostRenderer.material.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, revealPosition, 0, 0));

            if (progress >= 1f)
            {
                foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
                {
                    foreach (Renderer renderer in cachedAttachment.Renderers)
                    {
                        if (renderer == null || renderer.material == null) continue;
                        renderer.material.SetFloat(REVEAL_ENABLED_SHADER_ID, 0f);
                    }
                }

                avatarGhostComponent.Phase = AvatarGhostPhase.Hidden;
                avatarGhostComponent.PhaseElapsed = 0f;

                avatarBase.GhostGameObject.SetActive(false);
            }
        }
    }
}
