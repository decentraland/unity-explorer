using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.UI
{
    public struct PastePopupToastData
    {
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;

        public PastePopupToastData(Vector2 position, UniTask? closeTask = null)
        {
            Position = position;
            CloseTask = closeTask;
        }
    }
}
