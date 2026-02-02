using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.IO;
using System.Threading;
using Thirdweb;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebLoginService
    {
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;
        public IThirdwebWallet? ActiveWallet { get; private set; }
        private InAppWallet? pendingWallet;

        private readonly SemaphoreSlim mutex = new (1, 1);
        private readonly ThirdwebClient client;

        private UniTaskCompletionSource<bool>? loginCompletionSource;

        public ThirdWebLoginService(ThirdwebClient client, IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            this.web3AccountFactory = web3AccountFactory;
            this.client = client;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            string? email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, null);

            if (string.IsNullOrEmpty(email))
                return false;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                InAppWallet? wallet = await InAppWallet.Create(
                    client,
                    email,
                    storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

                if (!await wallet.IsConnected())
                    return false;

                ActiveWallet = wallet;
                ReportHub.Log(ReportCategory.AUTHENTICATION, "✅ ThirdWeb auto-login successful");
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"ThirdWeb auto-login failed with exception: {e.Message}");
                return false;
            }
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            if (ActiveWallet != null)
            {
                try { await ActiveWallet.Disconnect(); }
                finally { ActiveWallet = null; }
            }

            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL);
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(payload.Email))
                throw new ArgumentException("Email is required for OTP authentication", nameof(payload));

            await mutex.WaitAsync(ct);
            string email = payload.Email;

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                ActiveWallet = await OTPLoginFlowAsync(email, ct);

                string? sender = await ActiveWallet.GetAddress();

                IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                var ephemeralMessage =
                    $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {sessionExpiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

                string signature = await ActiveWallet!.PersonalSign(ephemeralMessage);

                var authChain = AuthChain.Create();
                authChain.SetSigner(sender.ToLower());

                authChain.Set(new AuthLink
                {
                    type = signature.Length == 132
                        ? AuthLinkType.ECDSA_EPHEMERAL
                        : AuthLinkType.ECDSA_EIP_1654_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = signature,
                });

                return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain);
            }
            catch (Exception)
            {
                await LogoutAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);

                mutex.Release();
            }
        }

        private async UniTask<InAppWallet> OTPLoginFlowAsync(string? email, CancellationToken ct)
        {
            pendingWallet = await InAppWallet.Create(
                client,
                email,
                storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

            await pendingWallet.SendOTP();
            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb login: OTP sent to email");

            // Wait for successful login via SubmitOtp
            loginCompletionSource = new UniTaskCompletionSource<bool>();
            ct.Register(() => loginCompletionSource?.TrySetCanceled(ct));

            await loginCompletionSource.Task;
            loginCompletionSource = null;
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: logged in as wallet {pendingWallet.WalletId}");

            // Store email for auto-login
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, email);

            ActiveWallet = pendingWallet;
            InAppWallet result = pendingWallet;
            pendingWallet = null;
            return result;
        }

        public async UniTask SubmitOtpAsync(string otp)
        {
            if (pendingWallet == null)
                throw new InvalidOperationException("SubmitOtp called but no pending wallet");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: validating OTP: {otp}");

            try
            {
                await pendingWallet.LoginWithOtp(otp);
                loginCompletionSource?.TrySetResult(true);
            }
            catch (InvalidOperationException e) when (e.Message.Contains("invalid or expired")) { throw new CodeVerificationException("Incorrect OTP code", e); }
        }

        public async UniTask ResendOtpAsync()
        {
            if (pendingWallet == null)
                throw new InvalidOperationException("ResendOtp called but no pending wallet");

            try { await pendingWallet.SendOTP(); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
                throw;
            }
        }
    }
}
