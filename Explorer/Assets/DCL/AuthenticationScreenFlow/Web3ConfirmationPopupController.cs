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
    ///     Drives the transaction/signing confirmation popup. Not registered in the MVC manager: the popup
    ///     is opened from the web3 provider callback while a request is in flight.
    ///     A social-login user cannot review a scene request in an external wallet, so anything that can
    ///     move assets asks for a second confirmation naming the recipient (scene creator, profile, or
    ///     outside wallet). When nothing can be named the first step shows the raw payload instead.
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

        public async UniTask<bool> ShowForResultAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            try
            {
                if (!NeedsFullReview(request))
                    return await view.ShowForResultAsync(request, null, null, ct);

                // The second step summarizes a mapped transfer, so the payload it was decoded from would
                // only add noise; an unmapped one has nothing else to show.
                if (!TryDecodeRecipient(request, out DecodedTransaction decoded))
                    return await view.ShowForResultAsync(request, TransactionRecipientUtils.UNKNOWN_REQUEST_DESCRIPTION, TransactionRawPayload.Format(request), ct);

                return await view.ShowForResultAsync(request, await DescribeRecipientAsync(decoded, ct), null, ct);
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

        private async UniTask<string?> DescribeRecipientAsync(DecodedTransaction decoded, CancellationToken ct)
        {
            // A request that pays the user's own wallet has no recipient to warn about.
            if (string.Equals(identityCache.Identity?.Address.ToString(), decoded.Recipient, StringComparison.OrdinalIgnoreCase))
                return null;

            string amount = TransactionRecipientUtils.Amount(decoded);

            // Checked before the profile lookup: a creator almost always has a profile too, which would
            // collapse every send to a creator into the generic profile copy.
            (bool enabled, string? creatorAddress, Vector2Int? baseParcel) = donationsService.DonationsEnabledCurrentScene.Value;

            if (enabled && string.Equals(creatorAddress, decoded.Recipient, StringComparison.OrdinalIgnoreCase))
                return TransactionRecipientUtils.SceneCreatorDescription(amount, await SceneNameFromParcelAsync(baseParcel, ct));

            Profile? profile = await profileRepository.GetAsync(decoded.Recipient, ct, IProfileRepository.FetchBehaviour.EnforceSingleGet);

            return profile != null
                ? TransactionRecipientUtils.ProfileDescription(amount, decoded.Recipient, profile.DisplayName)
                : TransactionRecipientUtils.ExternalWalletDescription(amount, decoded.Recipient);
        }

        private static bool TryDecodeRecipient(TransactionConfirmationRequest request, out DecodedTransaction decoded)
        {
            if (request.IsTypedDataSignature)
                return DecodedTransaction.TryFromMetaTransaction(request.TypedData, out decoded);

            decoded = DecodedTransaction.From(request.To, request.Value, request.Data);
            return decoded.Kind != TransactionKind.Unknown;
        }

        private async UniTask<string?> SceneNameFromParcelAsync(Vector2Int? parcel, CancellationToken ct)
        {
            if (!parcel.HasValue)
                return null;

            Result<string> sceneName = await donationsService.GetSceneNameAsync(parcel.Value, ct)
                                                            .SuppressToResultAsync(ReportCategory.AUTHENTICATION);

            return sceneName.Success ? sceneName.Value : null;
        }
    }
}
