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

    public readonly struct CharacterPreviewFactory : ICharacterPreviewFactory
    {
        private readonly IComponentPool<CharacterPreviewAvatarContainer> characterPreviewComponentPool;

        public CharacterPreviewFactory(IComponentPoolsRegistry poolsRegistry)
        {
            characterPreviewComponentPool = (IComponentPool<CharacterPreviewAvatarContainer>)poolsRegistry.GetPool(typeof(CharacterPreviewAvatarContainer));
        }

        public CharacterPreviewController Create(World world, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewCameraSettings cameraSettings)
        {
            var container = (CharacterPreviewAvatarContainer)characterPreviewComponentPool.Rent();
            container.Initialize(targetTexture);
            return new CharacterPreviewController(world, container, inputEventBus, characterPreviewComponentPool, cameraSettings);
        }
    }
}
