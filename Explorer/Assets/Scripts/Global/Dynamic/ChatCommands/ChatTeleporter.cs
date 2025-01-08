using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace Global.Dynamic.ChatCommands
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
        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        public ChatTeleporter(IRealmNavigator realmNavigator, ChatEnvironmentValidator environmentValidator, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.realmNavigator = realmNavigator;
            this.environmentValidator = environmentValidator;

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

        /// <summary>
        /// Parses the realm and teleports the player to it, with an optional target position.
        /// </summary>
        public async UniTask<string> TeleportToRealmAsync(string realm, Vector2Int? targetPosition, CancellationToken ct)
        {
            string realmAddress;

            if (realm.StartsWith("https"))
                realmAddress = realm;
            else if (!paramUrls.TryGetValue(realm, out realmAddress))
            {
                if (!realm.EndsWith(WORLD_SUFFIX))
                    realm += WORLD_SUFFIX;

                realmAddress = GetWorldAddress(realm);
            }

            var realmURL = URLDomain.FromString(realmAddress!);

            var environmentValidationResult = environmentValidator.ValidateTeleport(realmURL.ToString());

            if (!environmentValidationResult.Success)
                return environmentValidationResult.ErrorMessage!;

            var result = await realmNavigator.TryChangeRealmAsync(realmURL, ct, targetPosition ?? default);

            if (result.Success)
                return $"🟢 Welcome to the {realm} world!";

            var error = result.Error!.Value;

            return error.State switch
                   {
                       ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {realm} world!",
                       ChangeRealmError.SameRealm => $"🟡 You are already in {realm}!",
                       ChangeRealmError.NotReachable => $"🔴 Error. The world {realm} doesn't exist or not reachable!",
                       ChangeRealmError.ChangeCancelled => "🔴 Error. The operation was canceled!",
                       _ => throw new ArgumentOutOfRangeException()
                   };
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
                       TaskError.MessageError => $"🔴 Error. Teleport failed: {error.Message}",
                       TaskError.Timeout => "🔴 Error. Timeout",
                       TaskError.Cancelled => "🔴 Error. The operation was canceled!",
                       TaskError.UnexpectedException => $"🔴 Error. Teleport failed: {error.Message}",
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private string GetWorldAddress(string worldPath) =>
            worldDomain.Append(URLPath.FromString(worldPath)).Value;
    }
}
