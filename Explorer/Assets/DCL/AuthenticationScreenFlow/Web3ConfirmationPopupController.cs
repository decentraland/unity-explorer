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
    ///     A social-login user cannot review a scene request in an external wallet, so a request that can
    ///     move assets always asks for a second confirmation. That confirmation names who receives the
    ///     assets when the request maps to a known transfer shape: the creator of the current scene, a
    ///     Decentraland profile, or a wallet outside of Decentraland. When it maps to none of them there
    ///     is nothing to name, so the first step shows the raw payload for the user to check instead.
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
                if (!NeedsFullReview(request))
                    return await view.ShowAsync(request, null, null, ct);

                // A mapped transfer is summarized in plain language by the second step, so the payload it
                // was decoded from would only add noise; an unmapped one has nothing else to show.
                if (!TryDecodeRecipient(request, out DecodedTransaction decoded))
                    return await view.ShowAsync(request, TransactionRecipientUtils.UNKNOWN_REQUEST_DESCRIPTION, TransactionRawPayload.Format(request), ct);

                return await view.ShowAsync(request, await DescribeRecipientAsync(decoded, ct), null, ct);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
                return false;
            }
        }

        /// <summary>
        ///     Only a request that can move assets gets the raw payload and the second confirmation;
        ///     internal features (Gifting, Donations) state what they send in their own UI.
        /// </summary>
        private static bool NeedsFullReview(TransactionConfirmationRequest request) =>
            request.MovesAssets && !request.HideDetailsPanel;

        /// <summary>
        ///     The copy for the recipient confirmation step, or null when there is no recipient to confirm.
        /// </summary>
        private async UniTask<string?> DescribeRecipientAsync(DecodedTransaction decoded, CancellationToken ct)
        {
            // What makes a request worth a second confirmation is assets leaving the user: one that pays
            // the user's own wallet has no recipient to warn about.
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
            Profile? profile = await profileRepository.GetAsync(decoded.Recipient, ct, IProfileRepository.FetchBehaviour.EnforceSingleGet);

            return profile != null
                ? TransactionRecipientUtils.ProfileDescription(amount, decoded.Recipient, profile.DisplayName)
                : TransactionRecipientUtils.ExternalWalletDescription(amount, decoded.Recipient);
        }

        /// <summary>
        ///     A scene moves assets with a transaction or with a typed-data signature authorizing a
        ///     meta-transaction. False when neither maps to a shape naming a recipient.
        /// </summary>
        private static bool TryDecodeRecipient(TransactionConfirmationRequest request, out DecodedTransaction decoded)
        {
            if (request.IsTypedDataSignature)
                return TransactionRecipientDecoder.TryDecodeMetaTransaction(request.TypedData, out decoded);

            decoded = TransactionRecipientDecoder.Decode(request.To, request.Value, request.Data);
            return decoded.Kind != TransactionKind.Unknown;
        }

        private async UniTask<string?> SceneNameAsync(Vector2Int parcel, CancellationToken ct)
        {
            Result<string> sceneName = await donationsService.GetSceneNameAsync(parcel, ct)
                                                            .SuppressToResultAsync(ReportCategory.AUTHENTICATION);

            return sceneName.Success ? sceneName.Value : null;
        }
    }
}
