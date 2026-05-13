using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     ScriptableObject holding all named face expressions available to avatars.
    ///     Configure via Assets &gt; Create &gt; DCL &gt; Avatar &gt; Face Expression Config.
    ///     Expressions are the base layer of the face, underneath blink and mouth-pose animations
    ///     which temporarily override eyes and mouth respectively.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarFaceExpressionConfig", menuName = "DCL/Avatar/Face Expression Config")]
    public class AvatarFaceExpressionConfig : ScriptableObject
    {
        public AvatarFaceExpressionDefinition[] Expressions = Array.Empty<AvatarFaceExpressionDefinition>();
    }
}