using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS;
using ECS.Abstract;
using MVC;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AUTHENTICATION)]
    public partial class LoginFromDebugPanelSystem : BaseUnityLoopSystem
    {
        private const decimal WEI_FACTOR = 1_000_000_000_000_000_000;

        private readonly ICompositeWeb3Provider web3Authenticator;
        private readonly IMVCManager mvcManager;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache identityCache;
        private readonly DebugWidgetVisibilityBinding? widgetVisibility;

        private readonly ElementBinding<string> txRecipientBinding = new (string.Empty);
        private readonly ElementBinding<string> txAmountBinding = new ("1");
        private readonly ElementBinding<string> txTokenBinding = new (string.Empty);
        private readonly ElementBinding<string> txStatusBinding = new (string.Empty);

        private CancellationTokenSource? cancellationTokenSource;
        private CancellationTokenSource? txCancellationTokenSource;

        public LoginFromDebugPanelSystem(World world,
            IDebugContainerBuilder debugContainerBuilder,
            ICompositeWeb3Provider web3Authenticator,
            IMVCManager mvcManager,
            IRealmData realmData,
            IWeb3IdentityCache identityCache)
            : base(world)
        {
            this.web3Authenticator = web3Authenticator;
            this.mvcManager = mvcManager;
            this.realmData = realmData;
            this.identityCache = identityCache;

            debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.WEB3_AUTHENTICATION)
                                ?.SetVisibilityBinding(widgetVisibility = new DebugWidgetVisibilityBinding(false))
                                 .AddSingleButton("Login", Login)
                                 .AddSingleButton("Open Auth UI", OpenAuthenticationFlow)
                                 .AddSingleButton("Logout", Logout)
                                 .AddControl(new DebugConstLabelDef("TX Recipient (0x)"), new DebugTextFieldDef(txRecipientBinding))
                                 .AddControl(new DebugConstLabelDef("TX Amount"), new DebugTextFieldDef(txAmountBinding))
                                 .AddControl(new DebugConstLabelDef("TX Token (blank = native)"), new DebugTextFieldDef(txTokenBinding))
                                 .AddSingleButton("Send Test Tx", SendTestTransaction)
                                 .AddSingleButton("Send Test Tx To Self", SendTestTransactionToSelf)
                                 .AddCustomMarker("TX Status", txStatusBinding);
        }

        protected override void Update(float t)
        {
            widgetVisibility?.SetVisible(realmData.Configured);
        }

        private void Logout()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LogoutAsync(cancellationTokenSource.Token).Forget();
        }

        private void Login()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LoginAsync(LoginPayload.ForDappFlow(LoginMethod.GOOGLE), cancellationTokenSource.Token).Forget();
        }

        private void OpenAuthenticationFlow()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand()).Forget();
        }

        private void SendTestTransaction() =>
            SendTestTransactionAsync(txRecipientBinding.Value).Forget();

        private void SendTestTransactionToSelf() =>
            SendTestTransactionAsync(identityCache.Identity?.Address.ToString() ?? string.Empty).Forget();

        /// <summary>
        ///     Fires an eth_sendTransaction through the same SDKScene path a scene would use, so the
        ///     recipient gate is exercised. Only meaningful on the embedded (ThirdWeb/social-login) wallet.
        ///     Confirming broadcasts a real transaction on the active network; cancel to only inspect the popup.
        /// </summary>
        private async UniTaskVoid SendTestTransactionAsync(string recipient)
        {
            txCancellationTokenSource = txCancellationTokenSource.SafeRestart();
            CancellationToken ct = txCancellationTokenSource.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    txStatusBinding.Value = "Recipient address is required";
                    return;
                }

                if (!decimal.TryParse(txAmountBinding.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
                {
                    txStatusBinding.Value = $"Invalid amount: {txAmountBinding.Value}";
                    return;
                }

                var weiAmount = new BigInteger(decimal.Round(amount * WEI_FACTOR, 0, MidpointRounding.AwayFromZero));
                string from = identityCache.Identity?.Address.ToString() ?? string.Empty;
                string tokenContract = txTokenBinding.Value?.Trim() ?? string.Empty;

                string to;
                string value;
                string data;

                if (string.IsNullOrEmpty(tokenContract))
                {
                    // Native transfer: the recipient goes straight into `to`.
                    to = recipient;
                    value = "0x" + weiAmount.ToString("x");
                    data = "0x";
                }
                else
                {
                    // ERC-20 transfer: `to` is the token contract, the recipient lives in the calldata.
                    to = tokenContract;
                    value = "0x0";
                    data = EncodeErc20Transfer(recipient, weiAmount);
                }

                // params[0] is the transaction object, exactly as an SDK scene sends it. It must be a real
                // JSON object, not a serialized string: the Dapp web-signer path forwards params verbatim,
                // so a string reaches the signer as a quoted blob it cannot read ("contract address not
                // found"). A JObject serializes correctly for both the Dapp and ThirdWeb paths.
                var txObject = new JObject
                {
                    ["from"] = from,
                    ["to"] = to,
                    ["value"] = value,
                    ["data"] = data,
                };

                var request = new EthApiRequest
                {
                    id = Guid.NewGuid().GetHashCode(),
                    method = "eth_sendTransaction",
                    @params = new object[] { txObject },
                };

                txStatusBinding.Value = "Awaiting confirmation...";

                EthApiResponse response = await web3Authenticator.SendAsync(request, Web3RequestSource.SDKScene, ct);
                txStatusBinding.Value = $"Sent: {response.result}";
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                txStatusBinding.Value = $"Rejected/failed: {e.Message}";
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"Debug test transaction failed: {e.Message}");
            }
        }

        // ERC-20 transfer(address,uint256): selector + padded recipient + padded amount.
        private static string EncodeErc20Transfer(string to, BigInteger amount)
        {
            string cleanTo = to.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? to[2..] : to;
            string paddedTo = cleanTo.ToLowerInvariant().PadLeft(64, '0');
            string paddedAmount = amount.ToString("x").TrimStart('0').PadLeft(64, '0');
            return "0xa9059cbb" + paddedTo + paddedAmount;
        }
    }
}
