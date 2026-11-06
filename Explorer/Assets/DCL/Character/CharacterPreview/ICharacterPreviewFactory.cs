using Arch.Core;
using DCL.Optimization.Pools;
using Global.AppArgs;
using System;
using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     --
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world,
            RectTransform renderImage,
            RenderTexture targetTexture,
            CharacterPreviewInputEventBus inputEventBus,
            CharacterPreviewCameraSettings cameraSettings,
            Vector3 position);
    }

    public class CharacterPreviewFactory : ICharacterPreviewFactory
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IAppArgs appArgs;

        private IComponentPool<CharacterPreviewAvatarContainer>? characterPreviewComponentPool;
        private IComponentPool<Transform>? transformPool;

        public CharacterPreviewFactory(IComponentPoolsRegistry poolsRegistry, IAppArgs appArgs)
        {
            componentPoolsRegistry = poolsRegistry;
            this.appArgs = appArgs;
        }

        public CharacterPreviewController Create(World world,
            RectTransform renderImage,
            RenderTexture targetTexture,
            CharacterPreviewInputEventBus inputEventBus,
            CharacterPreviewCameraSettings cameraSettings,
            Vector3 position)
        {
            if (characterPreviewComponentPool == null && !componentPoolsRegistry.TryGetReferenceTypePool(out characterPreviewComponentPool))
                throw new InvalidOperationException($"No component pool registered for {nameof(CharacterPreviewAvatarContainer)}");

            if (transformPool == null && !componentPoolsRegistry.TryGetReferenceTypePool(out transformPool))
                throw new InvalidOperationException($"No component pool registered for {nameof(Transform)}");

            CharacterPreviewAvatarContainer container = characterPreviewComponentPool.Get()!;
            container.Initialize(targetTexture, position);
            return new CharacterPreviewController(world, renderImage, container, inputEventBus, characterPreviewComponentPool, cameraSettings, transformPool, appArgs);
        }
    }
}
