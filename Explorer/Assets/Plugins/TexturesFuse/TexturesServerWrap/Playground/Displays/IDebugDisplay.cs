using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays
{
    public abstract class AbstractDebugDisplay : MonoBehaviour
    {
        public abstract void Display(Texture2D texture);
    }
}
