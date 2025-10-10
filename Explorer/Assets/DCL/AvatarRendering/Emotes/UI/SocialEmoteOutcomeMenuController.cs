using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using Utility;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class SocialEmoteOutcomeMenuController : ControllerBase<SocialEmoteOutcomeMenuView, SocialEmoteOutcomeMenuController.SocialEmoteOutcomeMenuParams>
    {
        public class SocialEmoteOutcomeMenuParams
        {
            public string InteractingUserWalletAddress;
            public string Username;
            public Color UsernameColor;
            public bool IsCloseEnoughToAvatar;
        }

        private CancellationTokenSource cts;

        public SocialEmoteOutcomeMenuController([NotNull] ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        protected override void OnViewShow()
        {
          //  public void Show(string interactingUserWalletAddress, string username, bool isCloseEnoughToAvatar)
            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(inputData.InteractingUserWalletAddress);

            if (interaction.HasValue)
            {
                viewInstance!.RemoveAllChoices();

                for (int i = 0; i < interaction.Value.Emote.Model.Asset!.metadata.emoteDataADR287!.outcomes!.Length; ++i)
                    viewInstance.AddChoice(interaction.Value.Emote.Model.Asset.metadata.emoteDataADR287!.outcomes![i].title);

                viewInstance.SetEmoteTitle(interaction.Value.Emote.Model.Asset.metadata.name);

                if (inputData.IsCloseEnoughToAvatar)
                    viewInstance.HideDistanceMessage();
                else
                    viewInstance.ShowDistanceMessage(inputData.Username, inputData.UsernameColor);

                cts = cts.SafeRestart();
                viewInstance.ShowAsync(cts.Token).Forget();
            }

            base.OnViewShow();
        }
    }
}
