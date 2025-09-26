using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.RealmNavigation;
using DCL.Utility.Types;
using Global.AppArgs;
using System.Threading;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkHandle : IDeepLinkHandle
    {
        private readonly StartParcel startParcel;
        private readonly ChatTeleporter chatTeleporter;
        private readonly CancellationToken token;
        private readonly IAppArgsProcessor realmLaunchSettings;

        public DeepLinkHandle(StartParcel startParcel,
            ChatTeleporter chatTeleporter,
            CancellationToken token,
            IAppArgsProcessor realmLaunchSettings)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.realmLaunchSettings = realmLaunchSettings;
        }

        public string Name => "Real Implementation";

        public Result Handle(IAppArgs appArgs)
        {
            // Fixes: https://github.com/decentraland/unity-explorer/issues/5226
            // We need to re-apply launch settings to redirect the correct realm after authentication
            // TODO: Consider implementing a list of configurations to apply instead of focusing solely on the realm launch settings.
            realmLaunchSettings.ApplyConfig(appArgs);

            Vector2Int? position = PositionFrom(appArgs);
            URLDomain? realm = RealmFrom(appArgs);

            if (realm.HasValue)
            {
                chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position, token).Forget();
                return Result.SuccessResult();
            }

            if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token).Forget();
                else
                    startParcel.Assign(parcel);

                return Result.SuccessResult();
            }

            return Result.ErrorResult("no matches");
        }

        private static URLDomain? RealmFrom(IAppArgs appArgs)
        {
            if (!appArgs.TryGetValue(AppArgsFlags.REALM, out string? rawRealm))
                return null;

            if (string.IsNullOrEmpty(rawRealm))
                return null;

            return URLDomain.FromString(rawRealm);
        }

        private static Vector2Int? PositionFrom(IAppArgs appArgs)
        {
            if (!appArgs.TryGetValue(AppArgsFlags.POSITION, out string? rawPosition))
                return null;

            string[]? parts = rawPosition?.Split(',');

            if (parts == null || parts.Length < 2)
                return null;

            if (int.TryParse(parts[0], out int x) == false) return null;
            if (int.TryParse(parts[1], out int y) == false) return null;

            return new Vector2Int(x, y);
        }
    }
}
