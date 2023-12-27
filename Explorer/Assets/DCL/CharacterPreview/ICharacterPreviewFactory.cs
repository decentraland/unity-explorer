using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Check ICharacterPreviewFactory in the old renderer
    /// </summary>
    public interface ICharacterPreviewFactory
    {
        CharacterPreviewController Create(RenderTexture renderTexture /*add arguments*/);
    }
}
