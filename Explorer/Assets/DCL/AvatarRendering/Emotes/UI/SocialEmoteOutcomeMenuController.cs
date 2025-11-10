using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Input;
using DCL.Input.Component;
using MVC;
using System.Threading;
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

        private readonly World world;
        private readonly IInputBlock inputBlock;
        private readonly Entity playerEntity;
        private bool isInputEnabled;

        public SocialEmoteOutcomeMenuController(ViewFactoryMethod viewFactory, World world, IInputBlock inputBlock, Entity playerEntity) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.playerEntity = playerEntity;
            this.world = world;
        }

        private void OnOutcomePerformed(int outcomeIndex)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(inputData.InteractingUserWalletAddress);

            // Checks if the current emote has an outcome for the given index
            int outcomeCount = interaction!.Emote.Model.Asset!.metadata.socialEmoteData!.outcomes!.Length;

            if (outcomeIndex >= outcomeCount)
                return;

            if (interaction is { AreInteracting: false })
            {
                // Random outcome?
                if (outcomeIndex == 0 && interaction!.Emote.Model.Asset!.metadata.socialEmoteData!.randomizeOutcomes)
                {
                    outcomeIndex = Random.Range(0, outcomeCount);
                }

                TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent = new TriggerEmoteReactingToSocialEmoteIntent(
                        interaction.Emote.DTO.Metadata.id,
                        outcomeIndex,
                        interaction.InitiatorWalletAddress,
                        interaction.InitiatorEntity,
                        interaction.Id
                    );

                world.Add(playerEntity, triggerEmoteIntent);
            }
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            DCLInput.Instance.SocialEmoteOutcomes.Outcome1.performed += (_) => OnOutcomePerformed(0);
            DCLInput.Instance.SocialEmoteOutcomes.Outcome2.performed += (_) => OnOutcomePerformed(1);
            DCLInput.Instance.SocialEmoteOutcomes.Outcome3.performed += (_) => OnOutcomePerformed(2);
            EnableOutcomeInputs(false);
        }

        protected override void OnBeforeViewShow()
        {
            FillUI();
            UpdateDistanceMessage();
            viewInstance.Show();
        }

        protected override void OnViewClose()
        {
            EnableOutcomeInputs(false);
            viewInstance.Hide();
        }

        public void SetParams(SocialEmoteOutcomeMenuParams socialEmoteOutcomeMenuParams)
        {
            if (inputData.InteractingUserWalletAddress != socialEmoteOutcomeMenuParams.InteractingUserWalletAddress)
            {
                inputData.Username = socialEmoteOutcomeMenuParams.Username;
                FillUI();
            }

            if(inputData.IsCloseEnoughToAvatar != socialEmoteOutcomeMenuParams.IsCloseEnoughToAvatar)
            {
                inputData.UsernameColor = socialEmoteOutcomeMenuParams.UsernameColor;
                inputData.InteractingUserWalletAddress = socialEmoteOutcomeMenuParams.InteractingUserWalletAddress;
                inputData.IsCloseEnoughToAvatar = socialEmoteOutcomeMenuParams.IsCloseEnoughToAvatar;
                UpdateDistanceMessage();
            }
        }

        private void FillUI()
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(inputData.InteractingUserWalletAddress);

            if (interaction != null)
            {
                viewInstance!.ResetChoices();

                EmoteDTO.EmoteOutcomeDTO[] outcomes = interaction.Emote.Model.Asset!.metadata.socialEmoteData!.outcomes!;

                if (interaction.Emote.Model.Asset!.metadata.socialEmoteData!.randomizeOutcomes)
                {
                    // When outcomes are randomized, it only shows one option
                    viewInstance.AddChoice(viewInstance.RandomizedOutcomeText);
                }
                else
                {
                    for (int i = 0; i < outcomes.Length; ++i)
                        viewInstance.AddChoice(outcomes[i].title);
                }

                viewInstance.SetEmoteTitle(interaction.Emote.Model.Asset.metadata.name);
            }
        }

        private void UpdateDistanceMessage()
        {
            if (inputData.IsCloseEnoughToAvatar)
            {
                EnableOutcomeInputs(true);

                viewInstance.HideDistanceMessage();
            }
            else
            {
                EnableOutcomeInputs(false);

                viewInstance.ShowDistanceMessage(inputData.Username, inputData.UsernameColor);
            }
        }

        private void EnableOutcomeInputs(bool enable)
        {
            if(enable == isInputEnabled)
                return;

            if(enable)
                inputBlock.Enable(InputMapComponent.Kind.SOCIAL_EMOTE_OUTCOME_SELECTION);
            else
                inputBlock.Disable(InputMapComponent.Kind.SOCIAL_EMOTE_OUTCOME_SELECTION);

            isInputEnabled = enable;
        }
    }
}
