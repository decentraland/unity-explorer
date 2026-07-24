using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Donations;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    /// <summary>
    ///     Resolves the recipient of a scene-initiated transfer and translates it into a trust gate plus
    ///     plain-language display fields (see <see cref="RecipientGate" />). Reuses the donations service
    ///     for the scene creator address, which is already cross-checked against the Places API.
    /// </summary>
    public class TransactionRecipientResolver : ITransactionRecipientResolver
    {
        private const int NATIVE_DECIMALS = 18;
        private const int MANA_DECIMALS = 18;
        private const int MAX_FRACTION_DIGITS = 4;

        // Known Decentraland MANA contracts across networks (https://contracts.decentraland.org/addresses.json,
        // same source as DonationsService). MANA is recognised by any of these regardless of environment: this
        // is display-only ("50 MANA") and a scene may transfer MANA on either network.
        private static readonly HashSet<string> MANA_CONTRACTS = new (StringComparer.OrdinalIgnoreCase)
        {
            "0x0f5d2fb29fb7d3cfee444a200298f468908cc942", // Ethereum Mainnet
            "0xe7fdae84acaba2a5ba817b6e6d8a2d415dbfedbe", // Ethereum Goerli
            "0xfa04d2e2ba9aec166c93dfeeba7427b2303befa9", // Ethereum Sepolia
            "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", // Polygon Mainnet (PoS)
            "0x882da5967c435ea5cc6b09150d55e8304b838f45", // Polygon Mumbai Testnet
            "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", // Polygon Amoy Testnet
        };

        // The popup already labels gas/balance in "ETH"; keep the native transfer symbol consistent.
        private const string NATIVE_SYMBOL = "ETH";

        private readonly IProfileRepository profileRepository;
        private readonly IDonationsService donationsService;
        private readonly IWeb3IdentityCache identityCache;

        public TransactionRecipientResolver(
            IProfileRepository profileRepository,
            IDonationsService donationsService,
            IWeb3IdentityCache identityCache)
        {
            this.profileRepository = profileRepository;
            this.donationsService = donationsService;
            this.identityCache = identityCache;
        }

        public async UniTask ResolveAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            // Only scene-initiated transactions carry a recipient to gate. Signing requests are not
            // transactions, and internal (already-mediated) flows hide the details panel.
            if (!request.IsTransaction || request.HideDetailsPanel)
                return;

            DecodedTransaction decoded = TransactionRecipientDecoder.Decode(request.To, request.Value, request.Data);
            string recipient = decoded.Recipient;

            if (string.IsNullOrEmpty(recipient))
                return;

            request.RecipientAddress = recipient;
            FillAssetDisplay(request, decoded);

            // Sending to yourself needs no recipient warning.
            string? selfAddress = identityCache.Identity?.Address.ToString();

            if (!string.IsNullOrEmpty(selfAddress) && string.Equals(selfAddress, recipient, StringComparison.OrdinalIgnoreCase))
            {
                request.Gate = RecipientGate.None;
                return;
            }

            // Lvl 1: the recipient is the verified creator/donation wallet of the current scene.
            // This is checked before the profile lookup because a scene creator almost always also has
            // a Decentraland profile; checking the profile first would collapse every creator into the
            // generic profile gate and the scene-creator copy would never show.
            (bool enabled, string? creatorAddress, Vector2Int? baseParcel) = donationsService.DonationsEnabledCurrentScene.Value;

            if (enabled
                && !string.IsNullOrEmpty(creatorAddress)
                && string.Equals(creatorAddress, recipient, StringComparison.OrdinalIgnoreCase))
            {
                request.Gate = RecipientGate.SceneCreator;
                request.RecipientName = baseParcel.HasValue ? await SceneNameAsync(baseParcel.Value, ct) : null;
                return;
            }

            // Lvl 2: the recipient has a Decentraland profile.
            Profile? profile = await FetchProfileAsync(recipient, ct);

            if (profile != null)
            {
                request.Gate = RecipientGate.Profile;
                request.RecipientName = profile.DisplayName;
                return;
            }

            // Lvl 3: an address with no Decentraland identity.
            request.Gate = RecipientGate.External;
        }

        private async UniTask<Profile?> FetchProfileAsync(string recipient, CancellationToken ct)
        {
            try
            {
                return await profileRepository.GetAsync(recipient, ct, IProfileRepository.FetchBehaviour.ENFORCE_SINGLE_GET);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"Recipient profile lookup failed for {recipient}: {e.Message}");
                return null;
            }
        }

        private async UniTask<string?> SceneNameAsync(Vector2Int parcel, CancellationToken ct)
        {
            try
            {
                return await donationsService.GetSceneNameAsync(parcel, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"Scene name lookup failed: {e.Message}");
                return null;
            }
        }

        private void FillAssetDisplay(TransactionConfirmationRequest request, DecodedTransaction decoded)
        {
            switch (decoded.Kind)
            {
                case TransactionKind.NativeTransfer:
                    request.AmountDisplay = FormatUnits(decoded.Amount, NATIVE_DECIMALS);
                    request.AssetSymbol = NATIVE_SYMBOL;
                    break;
                case TransactionKind.Erc20Transfer when IsMana(decoded.TokenContract):
                    request.AmountDisplay = FormatUnits(decoded.Amount, MANA_DECIMALS);
                    request.AssetSymbol = "MANA";
                    break;
                default:
                    // Unknown token or opaque contract call: we cannot state an amount safely.
                    request.AmountDisplay = null;
                    request.AssetSymbol = null;
                    break;
            }
        }

        private static bool IsMana(string? tokenContract) =>
            !string.IsNullOrEmpty(tokenContract) && MANA_CONTRACTS.Contains(tokenContract!);

        private static string FormatUnits(BigInteger amount, int decimals)
        {
            if (amount.IsZero) return "0";
            if (decimals <= 0) return amount.ToString();

            BigInteger divisor = BigInteger.Pow(10, decimals);
            BigInteger whole = amount / divisor;
            BigInteger fraction = amount % divisor;

            if (fraction.IsZero)
                return whole.ToString();

            string fractionDigits = fraction.ToString().PadLeft(decimals, '0').TrimEnd('0');

            if (fractionDigits.Length > MAX_FRACTION_DIGITS)
                fractionDigits = fractionDigits.Substring(0, MAX_FRACTION_DIGITS);

            return fractionDigits.Length == 0 ? whole.ToString() : $"{whole}.{fractionDigits}";
        }
    }
}
