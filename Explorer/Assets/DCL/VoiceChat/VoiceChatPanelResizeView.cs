using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeView : MonoBehaviour
    {
        private const float ANIMATION_TIME = 0.2f;

        [SerializeField] private LayoutElement voiceChatPanelLayoutElement = null!;

        private CancellationTokenSource cts = new();
        private Vector2 cachedVector = Vector2.zero;

        public void Resize(float newHeight, bool instant = false)
        {
            cts = cts.SafeRestart();
            if (instant)
            {
                voiceChatPanelLayoutElement.preferredHeight = newHeight;
                return;
            }

            cachedVector.y = newHeight;
            voiceChatPanelLayoutElement.DOPreferredSize(cachedVector, ANIMATION_TIME).WithCancellation(cts.Token).Forget();
        }

        public async UniTask ResizeAsync(float newHeight)
        {
            cts = cts.SafeRestart();
            cachedVector.y = newHeight;
            await voiceChatPanelLayoutElement.DOPreferredSize(cachedVector, ANIMATION_TIME).WithCancellation(cts.Token);
        }
    }
}
