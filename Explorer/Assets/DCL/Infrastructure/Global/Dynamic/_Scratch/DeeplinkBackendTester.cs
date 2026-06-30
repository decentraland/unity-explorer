// THROWAWAY scratch tool — manual verification of deep-link sign-in backend communication.
// NOT production code. No style/architecture rules apply here. Do not commit / do not move into a prod path.
// Sits in a _Scratch subfolder next to BootstrapContainer.cs (which constructs DappWeb3Authenticator with the
// exact same deps), so it lands in Assembly-CSharp and every type resolves. Wrapped in #if UNITY_EDITOR so
// Unity never compiles it into a player build. Do not commit; delete when done.
//
// HOW TO USE (Play Mode required — socket.io, web requests, OpenUrl):
//   1. Empty scene -> empty GameObject -> attach this component.
//   2. Enter Play Mode (Awake builds the authenticator).
//   3. Right-click the component header (or the gear icon) to get the context menu, then run the steps in order.
//      Read the Unity console between steps.
//
// STEPS (context-menu items):
//   "1. Connect + Request"   -> full LoginViaDeeplinkAsync: socket connect to auth-api + emit "request"
//                               (mints requestId) + opens the browser with flow=deeplink. Reaching the browser
//                               proves the socket handshake worked. Then it BLOCKS waiting for an identityId.
//   "2. Dispatch identityId" -> paste an identityId into the Inspector field first, then run this to
//                               dispatcher.Dispatch(it). Replaces OS deep-link routing; unblocks step 1, which
//                               then does GET /identities/{id} internally (full E2E).
//   "3. Fetch identity by id"-> independent REST check: GET /identities/{id} via FetchIdentityByIdAsync directly
//                               (uses the Inspector identityId field).
//   "Cancel current"         -> cancels the in-flight operation and resets the token.

#if UNITY_EDITOR
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.RuntimeDeepLink;
using DCL.Time;
using DCL.Utility;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DeeplinkBackendTester : MonoBehaviour
{
    private const DecentralandEnvironment ENVIRONMENT = DecentralandEnvironment.Zone;

    [Tooltip("Paste the identityId here before running steps 2 and 3.")]
    [SerializeField] private string identityId = "";

    private DappWeb3Authenticator authenticator;
    private DeeplinkSigninDispatcher dispatcher;
    private IWebRequestController webRequestController;
    private CancellationTokenSource cts;
    private string authApiBaseUrl;

    private void Awake()
    {
        cts = new CancellationTokenSource();

        var urls = DecentralandUrlsSource.CreateForTest(ENVIRONMENT, ILaunchMode.PLAY);
        authApiBaseUrl = urls.Url(DecentralandUrl.ApiAuth);

        dispatcher = new DeeplinkSigninDispatcher();

        webRequestController = new WebRequestController(
            new WebRequestsAnalyticsContainer(),
            new MemoryWeb3IdentityCache(),
            new RequestHub(urls),
            new WebRequestBudget(int.MaxValue, new ElementBinding<ulong>(int.MaxValue)),
            new RealmClock());

        var webBrowser = new UnityAppWebBrowser(urls);

        authenticator = new DappWeb3Authenticator(
            webBrowser,
            URLAddress.FromString(urls.Url(DecentralandUrl.ApiAuth)),
            URLAddress.FromString(urls.Url(DecentralandUrl.AuthSignatureWebApp)),
            URLDomain.FromString(urls.Url(DecentralandUrl.ApiRpc)),
            new MemoryWeb3IdentityCache(),     // not touched in deeplink path
            new Web3AccountFactory(),
            new HashSet<string>(),             // whitelistMethods — not read in deeplink path
            new HashSet<string>(),             // readOnlyMethods — not read in deeplink path
            ENVIRONMENT,
            new NoopCodeVerificationFeatureFlag(),
            webRequestController,
            dispatcher,
            null);                             // identityExpirationDuration

        Debug.Log($"[DeeplinkBackendTester] Ready. env={ENVIRONMENT}\n" +
                  $"  auth-api  (socket): {urls.Url(DecentralandUrl.ApiAuth)}\n" +
                  $"  signature (browser): {urls.Url(DecentralandUrl.AuthSignatureWebApp)}\n" +
                  $"  rpc               : {urls.Url(DecentralandUrl.ApiRpc)}");
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        authenticator?.Dispose();
    }

    [ContextMenu("1. Connect + Request (LoginViaDeeplink)")]
    private void Step1_Login()
    {
        if (!EnsurePlayMode()) return;
        RunLoginAsync().Forget();
    }

    [ContextMenu("2. Dispatch identityId")]
    private void Step2_Dispatch()
    {
        if (!EnsurePlayMode()) return;

        string id = (identityId ?? "").Trim();

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[DeeplinkBackendTester] [2] identityId field is empty.");
            return;
        }

        Debug.Log($"[DeeplinkBackendTester] [2] dispatcher.Dispatch(\"{id}\")");
        dispatcher.Dispatch(id);
    }

    [ContextMenu("3. Fetch identity by id (GET /identities/{id})")]
    private void Step3_Fetch()
    {
        if (!EnsurePlayMode()) return;
        RunFetchAsync().Forget();
    }

    [ContextMenu("Cancel current")]
    private void CancelCurrent()
    {
        if (!EnsurePlayMode()) return;

        Debug.Log("[DeeplinkBackendTester] cancelling current operation, resetting token");
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
    }

    private async UniTaskVoid RunLoginAsync()
    {
        Debug.Log("[DeeplinkBackendTester] >> [1] LoginViaDeeplinkAsync START (socket connect + emit request + open browser, then waits for dispatch)");

        try
        {
            IWeb3Identity identity = await authenticator.LoginViaDeeplinkAsync(LoginPayload.ForDappFlow(LoginMethod.GOOGLE), cts.Token);
            Debug.Log($"[DeeplinkBackendTester] << [1] LOGIN OK. address={identity.Address} expiration={identity.Expiration:O} source={identity.Source} isExpired={identity.IsExpired}");
        }
        catch (OperationCanceledException) { Debug.Log("[DeeplinkBackendTester] << [1] cancelled"); }
        catch (Exception e) { Debug.LogError($"[DeeplinkBackendTester] << [1] EXCEPTION: {e.GetType().Name}: {e.Message}\n{e}"); }
    }

    private async UniTaskVoid RunFetchAsync()
    {
        string id = (identityId ?? "").Trim();

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[DeeplinkBackendTester] [3] identityId field is empty.");
            return;
        }

        Debug.Log($"[DeeplinkBackendTester] >> [3] FetchIdentityByIdAsync(\"{id}\") GET {authApiBaseUrl}/identities/{id}");

        try
        {
            IWeb3Identity identity = await authenticator.FetchIdentityByIdAsync(
                webRequestController, id, IWeb3Identity.Web3IdentitySource.Deeplink, cts.Token);

            Debug.Log($"[DeeplinkBackendTester] << [3] FETCH OK. address={identity.Address} expiration={identity.Expiration:O} source={identity.Source} isExpired={identity.IsExpired}");
        }
        catch (OperationCanceledException) { Debug.Log("[DeeplinkBackendTester] << [3] cancelled"); }
        catch (Exception e) { Debug.LogError($"[DeeplinkBackendTester] << [3] EXCEPTION: {e.GetType().Name}: {e.Message}\n{e}"); }
    }

    private bool EnsurePlayMode()
    {
        if (authenticator != null) return true;

        Debug.LogWarning("[DeeplinkBackendTester] Not initialized — enter Play Mode first (Awake builds the authenticator).");
        return false;
    }

    private sealed class NoopCodeVerificationFeatureFlag : DappWeb3Authenticator.ICodeVerificationFeatureFlag
    {
        public bool ShouldWaitForCodeVerificationFromServer => false;
    }
}
#endif
