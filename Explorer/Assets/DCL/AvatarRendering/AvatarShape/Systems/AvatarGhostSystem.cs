using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.CharacterPreview.Components;
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

        // Normal (0,1,0): keep below plane → bottom-to-top appearance (initial ghost reveal)
        private static readonly Vector4 REVEAL_NORMAL_DEFAULT = new (0, 1, 0, 0);

        // Normal (0,-1,0): keep above plane → bottom-to-top disappearance (ghost fades out during transition)
        private static readonly Vector4 REVEAL_NORMAL_FLIPPED = new (0, -1, 0, 0);

        // Feet-relative offsets (0 = feet, ~2 = head for a 2 m avatar).
        // Ghost shader uses object space; wearable shaders use world space (avatarBase.y + offset).
        private const float HIDE_OFFSET = -0.05f;
        private const float REVEAL_OFFSET = 2.05f;

        // Time to reveal the ghost
        internal const float REVEAL_DURATION_SEC = 0.8f;

        // Time transitioning from ghost to avatar
        internal const float HIDE_DURATION_SEC = 0.5f;

        private readonly Material ghostMaterialTemplate;

        internal AvatarGhostSystem(World world, Material ghostMaterialTemplate) : base(world)
        {
            this.ghostMaterialTemplate = ghostMaterialTemplate;
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
        [None(typeof(DeleteEntityIntention), typeof(AvatarGhostComponent), typeof(CharacterPreviewComponent))]
        private void EnsureGhostAvatar(in Entity entity, ref AvatarBase avatarBase, Profile profile)
        {
            // Instantiate once per avatar so subsequent SetVector calls never trigger Unity material copies
            var ghostMaterial = new Material(ghostMaterialTemplate);
            avatarBase.GhostRenderer.sharedMaterial = ghostMaterial;

            ghostMaterial.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, HIDE_OFFSET, 0, 0));
            ghostMaterial.SetColor(COLOR_SHADER_ID, profile!.UserNameColor);
            ghostMaterial.SetVector(REVEAL_NORMAL_SHADER_ID, REVEAL_NORMAL_DEFAULT);

            avatarBase.GhostGameObject.SetActive(true);

            World.Add(entity, new AvatarGhostComponent(ghostMaterial));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HideNewlyInstantiatedWearables(ref AvatarShapeComponent avatarShapeComponent, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.WearablesHidden) return;
            if (!avatarShapeComponent.IsWearableInstantiated) return;

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    renderer.sharedMaterial.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, HIDE_OFFSET, 0, 0));
                    renderer.sharedMaterial.SetFloat(REVEAL_ENABLED_SHADER_ID, 1f);
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

            // Flip the ghost normal so it disappears bottom-to-top while wearables reveal bottom-to-top
            avatarGhostComponent.GhostMaterial.SetVector(REVEAL_NORMAL_SHADER_ID, REVEAL_NORMAL_FLIPPED);
            avatarGhostComponent.Phase = AvatarGhostPhase.FullAvatarRevealing;
            avatarGhostComponent.PhaseElapsed = 0f;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateGhostRevealAnimation([Data] float deltaTime, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.GhostRevealingTransition) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / REVEAL_DURATION_SEC);
            float ghostRevealY = Mathf.Lerp(HIDE_OFFSET, REVEAL_OFFSET, progress);

            avatarGhostComponent.GhostMaterial.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, ghostRevealY, 0, 0));

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
            if (avatarGhostComponent.Phase != AvatarGhostPhase.FullAvatarRevealing) return;

            avatarGhostComponent.PhaseElapsed += deltaTime;
            float progress = Mathf.Clamp01(avatarGhostComponent.PhaseElapsed / HIDE_DURATION_SEC);
            float ghostRevealY = Mathf.Lerp(HIDE_OFFSET, REVEAL_OFFSET, progress);

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    renderer.sharedMaterial.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, ghostRevealY, 0, 0));
                }
            }

            avatarGhostComponent.GhostMaterial.SetVector(REVEAL_POSITION_SHADER_ID, new Vector4(0, ghostRevealY, 0, 0));

            if (progress >= 1f)
            {
                foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
                {
                    foreach (Renderer renderer in cachedAttachment.Renderers)
                    {
                        if (renderer == null || renderer.sharedMaterial == null) continue;
                        renderer.sharedMaterial.SetFloat(REVEAL_ENABLED_SHADER_ID, 0f);
                    }
                }

                avatarGhostComponent.Phase = AvatarGhostPhase.Hidden;
                avatarGhostComponent.PhaseElapsed = 0f;

                avatarBase.GhostGameObject.SetActive(false);
            }
        }
    }
}
