using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     ScriptableObject that holds all named face expressions available to avatars.
    ///     Configure via Assets > Create > DCL > Avatar > Face Expression Config.
    ///     Expressions are applied as the base layer of the face, underneath the blink
    ///     and mouth pose systems which temporarily override eyes and mouth respectively.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarFaceExpressionConfig", menuName = "DCL/Avatar/Face Expression Config")]
    public class AvatarFaceExpressionConfig : ScriptableObject
    {
        public AvatarFaceExpressionDefinition[] Expressions = System.Array.Empty<AvatarFaceExpressionDefinition>();
    }
}
