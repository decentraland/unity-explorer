using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens;
using DCL.Utility.Types;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Handles teleporting players to parcels or realms.
    /// </summary>
    public class ChatTeleporter
    {
        private const string WORLD_SUFFIX = ".dcl.eth";
        private const string CANCELLED_MESSAGE = "🔴 Error: The operation was canceled!";

        private readonly IRealmNavigator realmNavigator;
        private readonly IScenesCache scenesCache;
        private readonly Dictionary<string, string> paramUrls;
        private readonly ChatEnvironmentValidator environmentValidator;
        private readonly StartParcel startParcel;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly URLDomain worldDomain;
        private readonly URLDomain genesisDomain;

        public ChatTeleporter(IRealmNavigator realmNavigator, ChatEnvironmentValidator environmentValidator, IDecentralandUrlsSource decentralandUrlsSource, IScenesCache scenesCache, StartParcel startParcel, IReadOnlyLoadingStatus loadingStatus, IMVCManager mvcManager)
        {
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.environmentValidator = environmentValidator;
            this.startParcel = startParcel;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            worldDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.WorldServer));
            genesisDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));

            paramUrls = new Dictionary<string, string>
            {
                { "genesis", genesisDomain.Value },
                { "goerli", IRealmNavigator.GOERLI_URL },
                { "goerli-old", IRealmNavigator.GOERLI_OLD_URL },
                { "stream", IRealmNavigator.STREAM_WORLD_URL },
                { "sdk", IRealmNavigator.SDK_TEST_SCENES_URL },
                { "test", IRealmNavigator.TEST_SCENES_URL },
            };
        }

        public async UniTask<string> TeleportToRealmAsync(string realm, CancellationToken ct, string? spawnPointName = null)
        {
            ExtractWorldData(realm, out URLDomain realmUrl, out bool isWorld);

            if(!ValidEnvironment(realmUrl, out string errorMessage))
                return errorMessage;

            if (TryStartAt(realmUrl, null, spawnPointName))
                return HeadingTo(realm);

            if (!await WaitForStartupTeleportAsync(ct))
                return CANCELLED_MESSAGE;

            if (realmNavigator.IsAlreadyOnRealm(realmUrl))
            {
                if (spawnPointName == null)
                    return $"🟡 You are already in {realm}!";

                return await TeleportToParcelAsync(scenesCache.CurrentParcel.Value, true, ct, spawnPointName);
            }

            var result = await realmNavigator.TryChangeRealmAsync(realmUrl, ct, default, isWorld, true, spawnPointName: spawnPointName);

            if (result.Success)
                return $"🟢 Welcome to the {realm} world!";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {realm} world!",
                       ChangeRealmError.NotReachable => $"🔴 Error: The world {realm} doesn't exist or not reachable!",
                       ChangeRealmError.ChangeCancelled => CANCELLED_MESSAGE,
                       ChangeRealmError.LocalSceneDevelopmentBlocked => "🔴 Error: Realm changes are not allowed in local scene development mode",
                       ChangeRealmError.UnauthorizedWorldAccess => "🔴 Error: User is not authorized to access the requested world",
                       ChangeRealmError.Timeout => "🔴 Error: We were unable to connect to the realm. Please verify your connection.",
                       ChangeRealmError.PasswordRequired => $"🔴 Error: The world {realm} requires a password to access",
                       ChangeRealmError.PasswordCancelled => "🟡 Password entry was cancelled",
                       ChangeRealmError.WhitelistAccessDenied => $"🔴 Error: You are not on the access list for {realm}",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        /// <summary>
        /// Parses the realm and teleports the player to it, with an optional target position.
        /// </summary>
        public async UniTask<string> TeleportToRealmAsync(string realm, Vector2Int targetPosition, CancellationToken ct, string? spawnPointName = null)
        {
            ExtractWorldData(realm, out URLDomain realmUrl, out bool isWorld);

            if(!ValidEnvironment(realmUrl, out string errorMessage))
                return errorMessage;

            if (TryStartAt(realmUrl, targetPosition, spawnPointName))
                return HeadingTo(realm);

            if (!await WaitForStartupTeleportAsync(ct))
                return CANCELLED_MESSAGE;

            if(realmNavigator.IsAlreadyOnRealm(realmUrl))
                return await TeleportToParcelAsync(targetPosition, true, ct, spawnPointName);

            var result = await realmNavigator.TryChangeRealmAsync(realmUrl, ct, targetPosition, isWorld, spawnPointName: spawnPointName);

            if (result.Success)
                return $"🟢 Welcome to the {realm} world!";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {realm} world!",
                       ChangeRealmError.NotReachable => $"🔴 Error: The world {realm} doesn't exist or not reachable!",
                       ChangeRealmError.ChangeCancelled => CANCELLED_MESSAGE,
                       ChangeRealmError.LocalSceneDevelopmentBlocked => "🔴 Error: Realm changes are not allowed in local scene development mode",
                       ChangeRealmError.UnauthorizedWorldAccess => "🔴 Error: User is not authorized to access the requested world",
                       ChangeRealmError.Timeout => "🔴 Error: We were unable to connect to the realm. Please verify your connection.",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private bool ValidEnvironment(URLDomain realmUrl, out string errorMessage)
        {
            var environmentValidationResult = environmentValidator.ValidateTeleport(realmUrl.ToString());
            errorMessage = "";

            if (!environmentValidationResult.Success)
            {
                errorMessage = environmentValidationResult.ErrorMessage!;
                return false;
            }

            return true;
        }

        private void ExtractWorldData(string realm, out URLDomain realmUrl, out bool isWorld)
        {
            // 1) Already a URL => not a world
            if (realm.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                realmUrl = URLDomain.FromString(realm);
                isWorld = false;
                return;
            }

            // 2) Known param URL => not a world
            if (paramUrls.TryGetValue(realm, out string realmAddress))
            {
                realmUrl = URLDomain.FromString(realmAddress);
                isWorld = false;
                return;
            }

            // 3) Otherwise, treat as world and resolve address
            string worldName = realm;

            // Don't modify ENS names like your.world.eth
            // Convert short names like olavra => olavra.dcl.eth
            if (!worldName.IsEns() && !worldName.EndsWith(WORLD_SUFFIX, StringComparison.OrdinalIgnoreCase))
                worldName += WORLD_SUFFIX;

            string worldAddress = GetWorldAddress(worldName);

            realmUrl = URLDomain.FromString(worldAddress);
            isWorld = true;
        }


        /// <summary>
        /// Teleports the player to a parcel.
        /// </summary>
        public async UniTask<string> TeleportToParcelAsync(Vector2Int targetPosition, bool local, CancellationToken ct, string? spawnPointName = null)
        {
            if (TryStartAt(local ? null : genesisDomain, targetPosition, spawnPointName))
                return HeadingTo($"{targetPosition.x},{targetPosition.y}");

            if (!await WaitForStartupTeleportAsync(ct))
                return CANCELLED_MESSAGE;

            var result = await realmNavigator.TeleportToParcelAsync(targetPosition, ct, local, spawnPointName: spawnPointName);

            if (result.Success)
                return $"🟢 You teleported to {targetPosition.x},{targetPosition.y}.";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       TaskError.MessageError => $"🔴 Error: {error.Message}",
                       TaskError.Timeout => "🔴 Error: Timeout. Verify your connection.",
                       TaskError.Cancelled => CANCELLED_MESSAGE,
                       TaskError.UnexpectedException => $"🔴 Error: {error.Message}",
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private bool TryStartAt(URLDomain? realmUrl, Vector2Int? parcel, string? spawnPointName)
        {
            if (startParcel.IsConsumed()) return false;

            if (realmUrl is { } realm && !CanJoinStartupRealm(realm)) return false;

            return startParcel.TryTakeAsStartupDestination(realmUrl, parcel, spawnPointName);
        }

        // Once the startup realm is applied, only a link to the picked realm can still join the startup destination
        private bool CanJoinStartupRealm(URLDomain realm) =>
            !startParcel.IsRealmApplied || (realmNavigator.IsAlreadyOnRealm(realm) && (startParcel.Realm is not { } picked || picked == realm));

        private static string HeadingTo(string destination) =>
            $"🟢 Heading to {destination}!";

        // A request that could not become the startup destination runs after the startup teleport, never alongside it
        private async UniTask<bool> WaitForStartupTeleportAsync(CancellationToken ct)
        {
            if (IsIdleAfterStartup()) return true;

            bool cancelled = await UniTask.WaitUntil(IsIdleAfterStartup, cancellationToken: ct).SuppressCancellationThrow();
            return !cancelled;
        }

        // Reads false again during every later teleport, so a parked request never runs alongside one
        private bool IsIdleAfterStartup() =>
            startParcel.HasLanded
            && loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed
            && !mvcManager.IsShowing<SceneLoadingScreenView, SceneLoadingScreenController.Params>();

        private string GetWorldAddress(string worldPath) =>
            worldDomain.Append(URLPath.FromString(worldPath)).Value;
    }
}
