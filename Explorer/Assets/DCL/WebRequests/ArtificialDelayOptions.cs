using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using UnityEngine;
using Utility.Multithreading;
using Utility.Storage;

namespace DCL.WebRequests
{
    [CreateAssetMenu(fileName = "Web Artificial Delay", menuName = "DCL/Various/Web Artificial Delay")]
    public class ArtificialDelayOptions : ScriptableObject
    {
        [SerializeField] private bool use;
        [SerializeField] private float delaySeconds;

        [ContextMenu(nameof(Upload))]
        public void Upload()
        {
            ElementBindingOptions options = new ();
            use = options.Enable.Value;
            delaySeconds = options.Delay.Value;
        }

        [ContextMenu(nameof(Flush))]
        public void Flush()
        {
            new ElementBindingOptions().ApplyValues(use, delaySeconds);
        }

        public class ElementBindingOptions : ArtificialDelayWebRequestController.IReadOnlyOptions
        {
            public readonly IElementBinding<bool> Enable;
            public readonly IElementBinding<float> Delay;
            private readonly PersistentSetting<bool> enableSetting;
            private readonly PersistentSetting<float> delaySetting;

            public ElementBindingOptions() : this(
                PersistentSetting.CreateBool("webRequestsArtificialDelayEnable", false),
                PersistentSetting.CreateFloat("webRequestsArtificialDelaySeconds", 10)
            ) { }

            public ElementBindingOptions(PersistentSetting<bool> enableSetting, PersistentSetting<float> delaySetting)
            {
                this.enableSetting = enableSetting;
                this.delaySetting = delaySetting;
                Enable = new PersistentElementBinding<bool>(enableSetting);
                Delay = new PersistentElementBinding<float>(delaySetting);
            }

            public async UniTask<(float ArtificialDelaySeconds, bool UseDelay)> GetOptionsAsync()
            {
                await using (await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync())
                    return (Delay.Value, Enable.Value);
            }

            public void ApplyValues(bool enable, float delay)
            {
                enableSetting.ForceSave(enable);
                delaySetting.ForceSave(delay);
            }
        }
    }
}
