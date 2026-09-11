using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
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
        private readonly string? guestSessionIdOverride;
        public IThirdwebWallet? ActiveWallet { get; private set; }
        private InAppWallet? pendingWallet;
        private InAppWallet? pendingLinkWallet;
        private string pendingLinkEmail = string.Empty;

        private readonly DCLSemaphoreSlim mutex = new ();
        private readonly ThirdwebClient client;

        private UniTaskCompletionSource<bool>? loginCompletionSource;
        public event Action<string>? OTPSendSucceeded;

        private static string storageDirectoryPath => Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet");

        public ThirdWebLoginService(ThirdwebClient client, IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null,
            string? guestSessionIdOverride = null)
        {
            this.web3AccountFactory = web3AccountFactory;
            this.client = client;
            this.identityExpirationDuration = identityExpirationDuration;
            this.guestSessionIdOverride = guestSessionIdOverride;
        }

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            bool isGuest = DCLPlayerPrefs.GetBool(DCLPrefKeys.GUEST_SESSION_ACTIVE);
            string email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);

            ReportHub.LogProductionInfo($"[GuestLogin] auto-login: guestSessionActive={isGuest}, storedEmail='{Mask(email)}'");

            if (!isGuest && string.IsNullOrEmpty(email))
                return false;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(AUTO_LOGIN_TIMEOUT);
            CancellationToken linkedCt = timeoutCts.Token;

            try
            {
                await UniTask.SwitchToMainThread(linkedCt);

                InAppWallet wallet = isGuest
                    ? await CreateGuestWalletAsync(linkedCt)
                    : await CreateEmailWalletAsync(email, linkedCt);

                ct.ThrowIfCancellationRequested();

                if (linkedCt.IsCancellationRequested)
                {
                    ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"ThirdWeb auto-login timed out after {AUTO_LOGIN_TIMEOUT.TotalSeconds}s");
                    return false;
                }

                if (!await wallet.IsConnected().AsUniTask().AttachExternalCancellation(linkedCt))
                {
                    if (!isGuest)
                        return false;

                    await LoginWithGuestAsync(wallet, linkedCt);
                }

                if (isGuest)
                    await ThrowIfAccountWasUpgradedAsync(wallet, linkedCt);

                ActiveWallet = wallet;
                ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb auto-login successful");
                return true;
            }
            catch (OperationCanceledException)
            {
                // External cancellation — rethrow so caller knows it was cancelled
                throw;
            }

            // An upgraded account is not an auto-login failure: it propagates so the caller can require its OTP
            catch (Exception e) when (e is not GuestAccountUpgradedException)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"ThirdWeb auto-login failed with exception: {e.Message}");
                return false;
            }
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);

            if (ActiveWallet != null)
                try { await ActiveWallet.Disconnect().AsUniTask().AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                finally { ActiveWallet = null; }
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            bool isGuest = payload.Method == LoginMethod.GUEST;

            if (!isGuest && string.IsNullOrEmpty(payload.Email))
                throw new ArgumentException("Email is required for OTP authentication", nameof(payload));

            await mutex.WaitAsync(ct);

#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

            try
            {
                await UniTask.SwitchToMainThread(ct);

                InAppWallet wallet = isGuest
                    ? await GuestLoginFlowAsync(ct)
                    : await OTPLoginFlowAsync(payload.Email, ct);

                if (isGuest)
                    await ThrowIfAccountWasUpgradedAsync(wallet, ct);

                ActiveWallet = wallet;

                return await BuildIdentityAsync(wallet, isGuest ? LoginMethod.GUEST : LoginMethod.EMAIL_OTP, ct);
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

        private async UniTask<IWeb3Identity> BuildIdentityAsync(IThirdwebWallet wallet, LoginMethod method, CancellationToken ct)
        {
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

            return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain, method);
        }

        private async UniTask<InAppWallet> GuestLoginFlowAsync(CancellationToken ct)
        {
            InAppWallet wallet = await CreateGuestWalletAsync(ct);

            bool wasAlreadyConnected = await wallet.IsConnected().AsUniTask().AttachExternalCancellation(ct);

            ReportHub.LogProductionInfo($"[GuestLogin] guest wallet created: alreadyConnected={wasAlreadyConnected}, "
                                        + $"address='{(wasAlreadyConnected ? await wallet.GetAddress() : "<not connected>")}', storage='{storageDirectoryPath}'");

            await LoginWithGuestAsync(wallet, ct);

            ReportHub.LogProductionInfo($"[GuestLogin] logged in as guest: address='{await wallet.GetAddress()}', walletId='{wallet.WalletId}'");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: logged in as guest wallet {wallet.WalletId}");

            DCLPlayerPrefs.SetBool(DCLPrefKeys.GUEST_SESSION_ACTIVE, true, save: true);

            return wallet;
        }

        private UniTask<InAppWallet> CreateGuestWalletAsync(CancellationToken ct) =>
            InAppWallet.Create(client, authProvider: Thirdweb.AuthProvider.Guest, storageDirectoryPath: storageDirectoryPath)
                       .AsUniTask()
                       .AttachExternalCancellation(ct);

        private UniTask<InAppWallet> CreateEmailWalletAsync(string? email, CancellationToken ct) =>
            InAppWallet.Create(client, email, storageDirectoryPath: storageDirectoryPath)
                       .AsUniTask()
                       .AttachExternalCancellation(ct);

        private UniTask<string> LoginWithGuestAsync(InAppWallet wallet, CancellationToken ct)
        {
            string? sessionId = GuestSessionIdProvider.Resolve(guestSessionIdOverride);

            ReportHub.LogProductionInfo($"[GuestLogin] LoginWithGuest: override='{guestSessionIdOverride}', "
                                        + $"deviceIdSupported={SystemInfo.deviceUniqueIdentifier != SystemInfo.unsupportedIdentifier}, sessionId='{sessionId}'");

            return wallet.LoginWithGuest(sessionId)
                         .AsUniTask()
                         .AttachExternalCancellation(ct);
        }

        /// <summary>
        ///     The guest session id is derived from the device, so the guest flow keeps resolving the same wallet after
        ///     the account was upgraded through email linking. Connecting it only proves possession of the device, never
        ///     ownership of the linked email, so such an account is not signed in here: it needs its OTP.
        /// </summary>
        private async UniTask ThrowIfAccountWasUpgradedAsync(InAppWallet wallet, CancellationToken ct)
        {
            string? linkedEmail = await ResolveLinkedEmailAsync(wallet, ct);

            if (string.IsNullOrEmpty(linkedEmail))
            {
                ReportHub.LogProductionInfo($"[GuestLogin] upgrade check passed, continuing as guest: address='{await wallet.GetAddress()}'");
                return;
            }

            ReportHub.LogProductionInfo($"[GuestLogin] upgrade check FAILED, an email is linked to this account: address='{await wallet.GetAddress()}'");

            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb: the guest wallet has a linked email, the account can only be signed in with its OTP");

            throw new GuestAccountUpgradedException(linkedEmail);
        }

        private async UniTask<string?> ResolveLinkedEmailAsync(InAppWallet wallet, CancellationToken ct)
        {
            try
            {
                List<LinkedAccount>? linkedAccounts = await wallet.GetLinkedAccounts().AsUniTask().AttachExternalCancellation(ct);

                ReportHub.LogProductionInfo($"[GuestLogin] linked accounts of '{await wallet.GetAddress()}': count={linkedAccounts?.Count.ToString() ?? "<null>"}");

                if (linkedAccounts == null)
                    return null;

                foreach (LinkedAccount account in linkedAccounts)
                    ReportHub.LogProductionInfo($"[GuestLogin]   linked account: type='{account.Type}', email='{Mask(account.Details.Email)}', "
                                                + $"phone='{Mask(account.Details.Phone)}', address='{account.Details.Address}'");

                foreach (LinkedAccount account in linkedAccounts)
                    if (!string.IsNullOrEmpty(account.Details.Email))
                        return account.Details.Email;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION,
                    $"ThirdWeb: linked accounts of the guest wallet could not be read: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Keeps enough of a value to tell entries apart in a log without writing the whole address.
        /// </summary>
        private static string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int at = value.IndexOf('@');

            return at > 0
                ? $"{value[0]}***{value[at..]}"
                : $"{value[0]}***";
        }

        private async UniTask<InAppWallet> OTPLoginFlowAsync(string? email, CancellationToken ct)
        {
            if (email == null)
                throw new ArgumentException("Email is required for OTP authentication", nameof(email));

            pendingWallet = await CreateEmailWalletAsync(email, ct);

            try { await pendingWallet.SendOTP().AsUniTask().AttachExternalCancellation(ct); }
            catch (Exception ex) when (ContainsInvalidEmailError(ex)) { throw new InvalidEmailException(ex.Message, ex); }

            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb login: OTP sent to email");
            OTPSendSucceeded?.Invoke(email);

            // Wait for successful login via SubmitOtp
            loginCompletionSource = new UniTaskCompletionSource<bool>();
            ct.Register(() => loginCompletionSource?.TrySetCanceled(ct));

            await loginCompletionSource.Task;
            loginCompletionSource = null;
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: logged in as wallet {pendingWallet.WalletId}");

            // Store email for auto-login
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, email, save: true);

            ActiveWallet = pendingWallet;
            InAppWallet result = pendingWallet;
            pendingWallet = null;
            return result;
        }

        public async UniTask SubmitOtpAsync(string otp, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (pendingWallet == null)
                throw new InvalidOperationException("SubmitOtp called but no pending wallet");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: validating OTP: {otp}");

            try
            {
                await pendingWallet.LoginWithOtp(otp);
                                   // .AsUniTask().AttachExternalCancellation(ct); <- this breaks the flow for InvalidOperationEx
                loginCompletionSource?.TrySetResult(true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (InvalidOperationException e) when (e.Message.Contains("invalid or expired")) { throw new CodeVerificationException("Incorrect OTP code", e); }
        }

        public async UniTask SendEmailLinkOtpAsync(string email, CancellationToken ct)
        {
            InAppWallet walletToLink = await CreateEmailWalletAsync(email, ct);

            try { await walletToLink.SendOTP().AsUniTask().AttachExternalCancellation(ct); }
            catch (Exception ex) when (ContainsInvalidEmailError(ex)) { throw new InvalidEmailException(ex.Message, ex); }

            pendingLinkWallet = walletToLink;
            pendingLinkEmail = email;
            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb link: OTP sent to email");
        }

        public UniTask ResendEmailLinkOtpAsync(CancellationToken ct)
        {
            if (pendingLinkWallet == null)
                throw new InvalidOperationException("ResendEmailLinkOtp called but no pending link wallet");

            return pendingLinkWallet.SendOTP().AsUniTask().AttachExternalCancellation(ct);
        }

        public async UniTask<IWeb3Identity> LinkEmailAsync(string otp, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (ActiveWallet == null)
                throw new InvalidOperationException("LinkEmail called but no active wallet");

            if (pendingLinkWallet == null)
                throw new InvalidOperationException("LinkEmail called but no pending link wallet");

            var activeWallet = (InAppWallet)ActiveWallet;

            try { await activeWallet.LinkAccount(pendingLinkWallet, otp); }
            catch (Exception e) when (ContainsInvalidOtpError(e)) { throw new CodeVerificationException("Incorrect OTP code", e); }
            catch (Exception e) when (ContainsAlreadyLinkedError(e)) { throw new EmailAlreadyLinkedException("The email is already linked to another account", e); }

            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb link: email linked to the active wallet");

            DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, pendingLinkEmail, save: true);
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);

            pendingLinkWallet = null;
            pendingLinkEmail = string.Empty;

            return await BuildIdentityAsync(activeWallet, LoginMethod.EMAIL_OTP, ct);
        }

        private static bool ContainsInvalidOtpError(Exception ex) =>
            ContainsMessage(ex, "invalid or expired");

        private static bool ContainsAlreadyLinkedError(Exception ex) =>
            ContainsMessage(ex, "already linked")
            || ContainsMessage(ex, "already been linked")
            || ContainsMessage(ex, "already associated")
            || ContainsMessage(ex, "ACCOUNT_ALREADY_LINKED");

        private static bool ContainsMessage(Exception ex, string message)
        {
            Exception? current = ex;

            while (current != null)
            {
                if (current.Message.Contains(message, StringComparison.OrdinalIgnoreCase))
                    return true;

                current = current.InnerException;
            }

            return false;
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

            if (pendingWallet == null)
                throw new InvalidOperationException("ResendOtp called but no pending wallet");

            try { await pendingWallet.SendOTP().AsUniTask().AttachExternalCancellation(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
                throw;
            }
        }
    }
}
