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

        public void Resize(float newHeight, bool instant = false)
        {
            cts = cts.SafeRestart();
            if (instant)
            {
                voiceChatPanelLayoutElement.preferredHeight = newHeight;
                return;
            }

            var height = new Vector2(0, newHeight);
            voiceChatPanelLayoutElement.DOPreferredSize(height, ANIMATION_TIME).WithCancellation(cts.Token);
        }
    }
}
