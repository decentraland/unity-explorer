using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Donations;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Drives the transaction/signing confirmation popup. It is not registered in the MVC manager: the
    ///     popup is opened from the web3 provider callback while a request is in flight.
    ///     A social-login user cannot review a scene transaction in an external wallet, so a scene-initiated
    ///     transfer asks for a second confirmation naming who receives the assets: the creator of the
    ///     current scene, a Decentraland profile, or a wallet outside of Decentraland.
    /// </summary>
    public class Web3ConfirmationPopupController
    {
        private readonly Web3ConfirmationPopupView view;
        private readonly IProfileRepository profileRepository;
        private readonly IDonationsService donationsService;
        private readonly IWeb3IdentityCache identityCache;

        public Web3ConfirmationPopupController(
            Web3ConfirmationPopupView view,
            IProfileRepository profileRepository,
            IDonationsService donationsService,
            IWeb3IdentityCache identityCache)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.donationsService = donationsService;
            this.identityCache = identityCache;
        }

        public async UniTask<bool> ShowAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            try
            {
                string? recipientDescription = await ResolveRecipientDescriptionAsync(request, ct);
                return await view.ShowAsync(request, recipientDescription, ct);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
                return false;
            }
        }

        /// <summary>
        ///     The copy for the recipient confirmation step, or null when there is no recipient to confirm.
        /// </summary>
        private async UniTask<string?> ResolveRecipientDescriptionAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            // Only scene-initiated transactions carry a recipient to confirm: signing requests move no
            // assets, and internal features (Gifting, Donations) name the recipient in their own UI.
            if (!request.IsTransaction || request.HideDetailsPanel)
                return null;

            DecodedTransaction decoded = TransactionRecipientDecoder.Decode(request.To, request.Value, request.Data);

            if (string.IsNullOrEmpty(decoded.Recipient))
                return null;

            // Sending to yourself.
            if (string.Equals(identityCache.Identity?.Address.ToString(), decoded.Recipient, StringComparison.OrdinalIgnoreCase))
                return null;

            string amount = TransactionRecipientUtils.Amount(decoded);

            // The verified creator wallet of the current scene is checked before the profile lookup: a
            // creator almost always has a profile too, which would collapse every send to a creator into
            // the generic profile copy.
            (bool enabled, string? creatorAddress, Vector2Int? baseParcel) = donationsService.DonationsEnabledCurrentScene.Value;

            if (enabled && string.Equals(creatorAddress, decoded.Recipient, StringComparison.OrdinalIgnoreCase))
                return TransactionRecipientUtils.SceneCreatorDescription(amount, baseParcel.HasValue ? await SceneNameAsync(baseParcel.Value, ct) : null);

            // GetAsync suppresses its own failures and returns null, which reads as no profile.
            Profile? profile = await profileRepository.GetAsync(decoded.Recipient, ct, IProfileRepository.FetchBehaviour.ENFORCE_SINGLE_GET);

            return profile != null
                ? TransactionRecipientUtils.ProfileDescription(amount, decoded.Recipient, profile.DisplayName)
                : TransactionRecipientUtils.ExternalWalletDescription(amount, decoded.Recipient);
        }

        private async UniTask<string?> SceneNameAsync(Vector2Int parcel, CancellationToken ct)
        {
            Result<string> sceneName = await donationsService.GetSceneNameAsync(parcel, ct)
                                                            .SuppressToResultAsync(ReportCategory.AUTHENTICATION);

            return sceneName.Success ? sceneName.Value : null;
        }
    }
}
