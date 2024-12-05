﻿using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;
using Random = UnityEngine.Random;

namespace Global.Dynamic.ChatCommands
{
    public class GoToChatCommand : IChatCommand
    {
        private const string COMMAND_GOTO_LOCAL = "goto-local";
        private const string PARAMETER_RANDOM = "random";

        public static readonly Regex REGEX =
            new (
                $@"^/({ChatCommandsUtils.COMMAND_GOTO}|{COMMAND_GOTO_LOCAL})\s+(?:(-?\d+)\s*,\s*(-?\d+)|{PARAMETER_RANDOM})$",
                RegexOptions.Compiled);
        private readonly IRealmNavigator realmNavigator;

        private int x;
        private int y;

        public GoToChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            bool isLocal = match.Groups[1].Value == COMMAND_GOTO_LOCAL;
            ParseOrRandom(match);

            var teleportResult = await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), ct, isLocal);

            if (teleportResult.Success)
                return $"🟢 You teleported to {x},{y} in Genesis City";

            var error = teleportResult.Error.Value;

            return error.State switch
                   {
                       TaskError.MessageError => $"🔴 Error. Teleport failed: {error.Message}",
                       TaskError.Timeout => $"🔴 Error. Timeout",
                       TaskError.Cancelled => "🔴 Error. The operation was canceled!",
                       TaskError.UnexpectedException => $"🔴 Error. Teleport failed: {error.Message}",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private void ParseOrRandom(Match match)
        {
            if (match.Groups[2].Success && match.Groups[3].Success)
            {
                x = int.Parse(match.Groups[2].Value);
                y = int.Parse(match.Groups[3].Value);
            }
            else // means it's equal "random"
            {
                x = Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x);
                y = Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y);
            }
        }
    }
}
