using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
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

        private readonly IRealmNavigator realmNavigator;
        private readonly Dictionary<string, string> paramUrls;
        private readonly ChatEnvironmentValidator environmentValidator;
        private readonly URLDomain worldDomain;

        public ChatTeleporter(IRealmNavigator realmNavigator, ChatEnvironmentValidator environmentValidator, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.realmNavigator = realmNavigator;
            this.environmentValidator = environmentValidator;
            worldDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.WorldServer));

            paramUrls = new Dictionary<string, string>
            {
                { "genesis", decentralandUrlsSource.Url(DecentralandUrl.Genesis) },
                { "goerli", IRealmNavigator.GOERLI_URL },
                { "goerli-old", IRealmNavigator.GOERLI_OLD_URL },
                { "stream", IRealmNavigator.STREAM_WORLD_URL },
                { "sdk", IRealmNavigator.SDK_TEST_SCENES_URL },
                { "test", IRealmNavigator.TEST_SCENES_URL },
            };
        }

        public async UniTask<string> TeleportToRealmAsync(string realm, CancellationToken ct)
        {
            ExtractWorldData(realm, out URLDomain realmURL, out bool isWorld);

            if(!ValidEnvironment(realmURL, out string errorMessage))
                return errorMessage;

            if (realmNavigator.IsAlreadyOnRealm(realmURL))
                return $"🟡 You are already in {realm}!";

            var result = await realmNavigator.TryChangeRealmAsync(realmURL, ct, default, isWorld, true);

            if (result.Success)
                return $"🟢 Welcome to the {realm} world!";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {realm} world!",
                       ChangeRealmError.NotReachable => $"🔴 Error: The world {realm} doesn't exist or not reachable!",
                       ChangeRealmError.ChangeCancelled => "🔴 Error: The operation was canceled!",
                       ChangeRealmError.LocalSceneDevelopmentBlocked => "🔴 Error: Realm changes are not allowed in local scene development mode",
                       ChangeRealmError.UnauthorizedWorldAccess => "🔴 Error: User is not authorized to access the requested world",
                       ChangeRealmError.Timeout => "🔴 Error: We were unable to connect to the realm. Please verify your connection.",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        /// <summary>
        /// Parses the realm and teleports the player to it, with an optional target position.
        /// </summary>
        public async UniTask<string> TeleportToRealmAsync(string realm, Vector2Int targetPosition, CancellationToken ct)
        {
            ExtractWorldData(realm, out URLDomain realmURL, out bool isWorld);

            if(!ValidEnvironment(realmURL, out string errorMessage))
                return errorMessage;

            if(realmNavigator.IsAlreadyOnRealm(realmURL))
                return await TeleportToParcelAsync(targetPosition, true, ct);

            var result = await realmNavigator.TryChangeRealmAsync(realmURL, ct, targetPosition, isWorld, false);

            if (result.Success)
                return $"🟢 Welcome to the {realm} world!";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {realm} world!",
                       ChangeRealmError.NotReachable => $"🔴 Error: The world {realm} doesn't exist or not reachable!",
                       ChangeRealmError.ChangeCancelled => "🔴 Error: The operation was canceled!",
                       ChangeRealmError.LocalSceneDevelopmentBlocked => "🔴 Error: Realm changes are not allowed in local scene development mode",
                       ChangeRealmError.UnauthorizedWorldAccess => "🔴 Error: User is not authorized to access the requested world",
                       ChangeRealmError.Timeout => "🔴 Error: We were unable to connect to the realm. Please verify your connection.",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private bool ValidEnvironment(URLDomain realmURL, out string errorMessage)
        {
            var environmentValidationResult = environmentValidator.ValidateTeleport(realmURL.ToString());
            errorMessage = "";

            if (!environmentValidationResult.Success)
            {
                errorMessage = environmentValidationResult.ErrorMessage;
                return false;
            }

            return true;
        }

        private void ExtractWorldData(string realm, out URLDomain realmURL, out bool isWorld)
        {
            // 1) Already a URL => not a world
            if (realm.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                realmURL = URLDomain.FromString(realm);
                isWorld = false;
                return;
            }

            // 2) Known param URL => not a world
            if (paramUrls.TryGetValue(realm, out string realmAddress))
            {
                realmURL = URLDomain.FromString(realmAddress);
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

            realmURL = URLDomain.FromString(worldAddress);
            isWorld = true;
        }


        /// <summary>
        /// Teleports the player to a parcel.
        /// </summary>
        public async UniTask<string> TeleportToParcelAsync(Vector2Int targetPosition, bool local, CancellationToken ct)
        {
            var result = await realmNavigator.TeleportToParcelAsync(targetPosition, ct, local);

            if (result.Success)
                return $"🟢 You teleported to {targetPosition.x},{targetPosition.y}.";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       TaskError.MessageError => $"🔴 Error: {error.Message}",
                       TaskError.Timeout => "🔴 Error: Timeout. Verify your connection.",
                       TaskError.Cancelled => "🔴 Error: The operation was canceled!",
                       TaskError.UnexpectedException => $"🔴 Error: {error.Message}",
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private string GetWorldAddress(string worldPath) =>
            worldDomain.Append(URLPath.FromString(worldPath)).Value;
    }
}
