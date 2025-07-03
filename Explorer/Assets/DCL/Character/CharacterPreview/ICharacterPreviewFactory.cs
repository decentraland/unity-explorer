using Arch.Core;
using DCL.Optimization.Pools;
using Global.AppArgs;
using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     --
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world, RectTransform renderImage, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewCameraSettings cameraSettings);
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

        public CharacterPreviewController Create(World world, RectTransform renderImage, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewCameraSettings cameraSettings)
        {
            characterPreviewComponentPool ??= componentPoolsRegistry.GetReferenceTypePool<CharacterPreviewAvatarContainer>();
            transformPool ??= componentPoolsRegistry.GetReferenceTypePool<Transform>();
            CharacterPreviewAvatarContainer container = characterPreviewComponentPool.Get();
            container.Initialize(targetTexture);
            return new CharacterPreviewController(world, renderImage, container, inputEventBus, characterPreviewComponentPool, cameraSettings, transformPool, appArgs);
        }
    }
}
