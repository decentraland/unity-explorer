// THROWAWAY scratch tool — manual verification of deep-link sign-in backend communication.
// NOT production code. No style/architecture rules apply here. Do not commit / do not move into a prod path.
// Sits in a _Scratch subfolder next to BootstrapContainer.cs (which constructs DappDeepLinkAuthenticator with the
// exact same deps), so it lands in Assembly-CSharp and every type resolves. Wrapped in #if UNITY_EDITOR so
// Unity never compiles it into a player build. Do not commit; delete when done.
//
// HOW TO USE (Play Mode required — web requests, OpenUrl):
//   1. Empty scene -> empty GameObject -> attach this component.
//   2. Enter Play Mode (Awake builds the authenticator).
//   3. Right-click the component header (or the gear icon) to get the context menu, then run the steps in order.
//      Read the Unity console between steps.
//
// STEPS (context-menu items):
//   "1. Login (deeplink)"    -> full LoginAsync: POST /requests (mints requestId over HTTP) + opens the browser with
//                               flow=deeplink. Reaching the browser proves the mint worked. Then it BLOCKS waiting
//                               for an identityId.
//   "2. Dispatch identityId" -> paste an identityId into the Inspector field first, then run this to
//                               dispatcher.Dispatch(it). Replaces OS deep-link routing; unblocks step 1, which
//                               then does GET /identities/{id} internally (full E2E).
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
using System.Threading;
using UnityEngine;

public class DeeplinkBackendTester : MonoBehaviour
{
    private const DecentralandEnvironment ENVIRONMENT = DecentralandEnvironment.Zone;

    [Tooltip("Paste the identityId here before running step 2.")]
    [SerializeField] private string identityId = "";

    private DappDeepLinkAuthenticator authenticator;
    private DeeplinkSigninDispatcher dispatcher;
    private IWebRequestController webRequestController;
    private CancellationTokenSource cts;

    private void Awake()
    {
        cts = new CancellationTokenSource();

        var urls = DecentralandUrlsSource.CreateForTest(ENVIRONMENT, ILaunchMode.PLAY);

        dispatcher = new DeeplinkSigninDispatcher();

        webRequestController = new WebRequestController(
            new WebRequestsAnalyticsContainer(),
            new MemoryWeb3IdentityCache(),
            new RequestHub(urls),
            new WebRequestBudget(int.MaxValue, new ElementBinding<ulong>(int.MaxValue)),
            new RealmClock());

        var webBrowser = new UnityAppWebBrowser(urls);

        authenticator = new DappDeepLinkAuthenticator(
            webBrowser,
            URLAddress.FromString(urls.Url(DecentralandUrl.ApiAuth)),
            URLAddress.FromString(urls.Url(DecentralandUrl.AuthSignatureWebApp)),
            new Web3AccountFactory(),
            webRequestController,
            dispatcher,
            null);                             // identityExpirationDuration

        Debug.Log($"[DeeplinkBackendTester] Ready. env={ENVIRONMENT}\n" +
                  $"  auth-api  (POST)   : {urls.Url(DecentralandUrl.ApiAuth)}\n" +
                  $"  signature (browser): {urls.Url(DecentralandUrl.AuthSignatureWebApp)}");
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        authenticator?.Dispose();
    }

    [ContextMenu("1. Login (deeplink)")]
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
        Debug.Log("[DeeplinkBackendTester] >> [1] LoginAsync START (POST /requests + open browser, then waits for dispatch)");

        try
        {
            IWeb3Identity identity = await authenticator.LoginAsync(LoginPayload.ForDappFlow(LoginMethod.GOOGLE), cts.Token);
            Debug.Log($"[DeeplinkBackendTester] << [1] LOGIN OK. address={identity.Address} expiration={identity.Expiration:O} source={identity.Source} isExpired={identity.IsExpired}");
        }
        catch (OperationCanceledException) { Debug.Log("[DeeplinkBackendTester] << [1] cancelled"); }
        catch (Exception e) { Debug.LogError($"[DeeplinkBackendTester] << [1] EXCEPTION: {e.GetType().Name}: {e.Message}\n{e}"); }
    }

    private bool EnsurePlayMode()
    {
        if (authenticator != null) return true;

        Debug.LogWarning("[DeeplinkBackendTester] Not initialized — enter Play Mode first (Awake builds the authenticator).");
        return false;
    }
}
#endif
