using Arch.Core;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.CharacterPreview
{    /// <summary>--
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world, IComponentPoolsRegistry poolsRegistry, RenderTexture targetTexture,  CharacterPreviewInputEventBus inputEventBus);
    }

    public class CharacterPreviewFactory : ICharacterPreviewFactory
    {
        public CharacterPreviewController Create(World world, IComponentPoolsRegistry poolsRegistry, RenderTexture targetTexture, CharacterPreviewInputEventBus inputEventBus)
        {
            var container = (CharacterPreviewContainer)poolsRegistry.GetPool(typeof(CharacterPreviewContainer)).Rent();
            container.Initialize(targetTexture);
            return new CharacterPreviewController(world, container, inputEventBus, poolsRegistry);
        }
    }
}
