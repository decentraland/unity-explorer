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
using Utility.Multithreading;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebLoginService
    {
        /// <summary>
        ///     Maximum time allowed for auto-login to complete.
        ///     Prevents indefinite waiting on the splash screen if ThirdWeb services are slow or unreachable.
        /// </summary>
        private static readonly TimeSpan AUTO_LOGIN_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;
        public IThirdwebWallet? ActiveWallet { get; private set; }
        private OtpLogin? pendingLogin;

        private readonly DCLSemaphoreSlim mutex = new ();
        private readonly ThirdwebClient client;

        public event Action<string>? OTPSendSucceeded;

        public ThirdWebLoginService(ThirdwebClient client, IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            this.web3AccountFactory = web3AccountFactory;
            this.client = client;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            string email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);

            if (string.IsNullOrEmpty(email))
                return false;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(AUTO_LOGIN_TIMEOUT);
            CancellationToken linkedCt = timeoutCts.Token;

            try
            {
                await UniTask.SwitchToMainThread(linkedCt);

                InAppWallet wallet = await InAppWallet.Create(
                    client,
                    email,
                    storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"))
                                                       .AsUniTask().AttachExternalCancellation(linkedCt);

                if (!await wallet.IsConnected().AsUniTask().AttachExternalCancellation(linkedCt))
                    return false;

                ct.ThrowIfCancellationRequested();
                ActiveWallet = wallet;
                ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb auto-login successful");
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"ThirdWeb auto-login failed with exception: {e.Message}");
                return false;
            }
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);

            if (ActiveWallet != null)
                try { await ActiveWallet.Disconnect().AsUniTask().AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                finally { ActiveWallet = null; }
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(payload.Email))
                throw new ArgumentException("Email is required for OTP authentication", nameof(payload));

            await mutex.WaitAsync(ct);
            string email = payload.Email;

#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

            try
            {
                await UniTask.SwitchToMainThread(ct);

                InAppWallet wallet = await OTPLoginFlowAsync(email, ct);
                ActiveWallet = wallet;

                string sender = await wallet.GetAddress().AsUniTask().AttachExternalCancellation(ct);

                IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                var ephemeralMessage =
                    $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {sessionExpiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

                string signature = await wallet.PersonalSign(ephemeralMessage).AsUniTask().AttachExternalCancellation(ct);

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

                return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain, IWeb3Identity.Web3IdentitySource.OTP);
            }
            catch (Exception)
            {
                await LogoutAsync(ct);
                throw;
            }
            finally
            {
#if !UNITY_WEBGL
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);
#else
                await UniTask.SwitchToMainThread(CancellationToken.None);
#endif

                mutex.Release();
            }
        }

        private async UniTask<InAppWallet> OTPLoginFlowAsync(string email, CancellationToken ct)
        {
            InAppWallet wallet = await InAppWallet.Create(
                client,
                email,
                storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"))
                                                 .AsUniTask().AttachExternalCancellation(ct);
            var login = new OtpLogin(wallet);
            pendingLogin = login;

            try
            {
                try { await wallet.SendOTP().AsUniTask().AttachExternalCancellation(ct); }
                catch (Exception ex) when (ContainsInvalidEmailError(ex)) { throw new InvalidEmailException(ex.Message, ex); }

                OTPSendSucceeded?.Invoke(email);
                await login.Completion.Task.AttachExternalCancellation(ct);
                ct.ThrowIfCancellationRequested();
                DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, email, save: true);
                return wallet;
            }
            finally { pendingLogin = null; }
        }

        public async UniTask SubmitOtpAsync(string otp, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            OtpLogin login = pendingLogin ?? throw new InvalidOperationException("SubmitOtp called without a pending login");

            try
            {
                await login.Wallet.LoginWithOtp(otp).AsUniTask().AttachExternalCancellation(ct);
                ct.ThrowIfCancellationRequested();
                login.Completion.TrySetResult();
            }
            catch (InvalidOperationException e) when (e.Message.Contains("invalid or expired")) { throw new CodeVerificationException("Incorrect OTP code", e); }
        }

        private static bool ContainsInvalidEmailError(Exception ex)
        {
            const string INVALID_EMAIL_MESSAGE = "Invalid email.";
            Exception? current = ex;

            while (current != null)
            {
                if (current.Message == INVALID_EMAIL_MESSAGE)
                    return true;

                current = current.InnerException;
            }

            return false;
        }

        public async UniTask ResendOtpAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            OtpLogin login = pendingLogin ?? throw new InvalidOperationException("ResendOtp called without a pending login");
            await login.Wallet.SendOTP().AsUniTask().AttachExternalCancellation(ct);
        }

        private sealed class OtpLogin
        {
            public readonly InAppWallet Wallet;
            public readonly UniTaskCompletionSource Completion = new ();

            public OtpLogin(InAppWallet wallet)
            {
                Wallet = wallet;
            }
        }
    }
}
