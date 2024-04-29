using Arch.Core;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     --
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewCameraSettings cameraSettings);
    }

    public class CharacterPreviewFactory : ICharacterPreviewFactory
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private IComponentPool<CharacterPreviewAvatarContainer>? characterPreviewComponentPool;

        public CharacterPreviewFactory(IComponentPoolsRegistry poolsRegistry)
        {
            componentPoolsRegistry = poolsRegistry;
        }

        public CharacterPreviewController Create(World world, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewCameraSettings cameraSettings)
        {
            characterPreviewComponentPool ??= componentPoolsRegistry.GetReferenceTypePool<CharacterPreviewAvatarContainer>();
            CharacterPreviewAvatarContainer container = characterPreviewComponentPool.Get();
            container.Initialize(targetTexture);
            return new CharacterPreviewController(world, container, inputEventBus, characterPreviewComponentPool, cameraSettings);
        }
    }
}
