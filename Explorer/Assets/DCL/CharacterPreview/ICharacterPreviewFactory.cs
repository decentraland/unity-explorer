using Arch.Core;
using System;
using UnityEngine;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewFactory : ICharacterPreviewFactory{

        public CharacterPreviewController Create(World world, CharacterPreviewContainer container) =>
            new (world, container);
    }

    /// <summary>
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(World world, CharacterPreviewContainer container /*add arguments*/);
    }
}
