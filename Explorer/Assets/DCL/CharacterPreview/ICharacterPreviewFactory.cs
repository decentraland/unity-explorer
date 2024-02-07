using Arch.Core;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.CharacterPreview
{    /// <summary>--
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world, RenderTexture targetTexture,  CharacterPreviewInputEventBus inputEventBus);
    }

    public readonly struct CharacterPreviewFactory : ICharacterPreviewFactory
    {
        private readonly IComponentPool<CharacterPreviewContainer> characterPreviewComponentPool;

        public CharacterPreviewFactory(IComponentPoolsRegistry poolsRegistry)
        {
            characterPreviewComponentPool = (IComponentPool<CharacterPreviewContainer>)poolsRegistry.GetPool(typeof(CharacterPreviewContainer));
        }

        public CharacterPreviewController Create(World world, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus)
        {
            var container = (CharacterPreviewContainer)characterPreviewComponentPool.Rent();
            container.Initialize(targetTexture);
            return new CharacterPreviewController(world, container, inputEventBus, characterPreviewComponentPool);
        }
    }
}
