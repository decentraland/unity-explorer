using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class TexturesFusePlayground : MonoBehaviour
    {
        private void Start()
        {
            var unzip = new TexturesUnzip();
            unzip.TextureFromBytes(new byte[10]);
        }
    }
}
