using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat
{
    internal class ChatCommandsHandler
    {
        private const char CHAT_COMMAND_CHAR = '/';

        private const string GOTO_COMMAND = "/goto";
        private const string WORLD_COMMAND = "/world";

        private const string WORLDS_SUFFIX = ".dcl.eth";

        private readonly URLDomain worldDomain = URLDomain.FromString("https://worlds-content-server.decentraland.org/world");

        private readonly Func<string, CancellationToken, UniTask> changeRealmAsync;


        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();

        private string GetWorldAddress(string message)
        {
            string worldPath = message.StartsWith(WORLD_COMMAND)
                ? message[(WORLD_COMMAND.Length + 1)..]
                : message[(GOTO_COMMAND.Length + 1)..];

            if (!worldAddressesCaches.TryGetValue(worldPath, out URLAddress address))
            {
                address = worldDomain.Append(URLPath.FromString(worldPath));
                worldAddressesCaches.Add(worldPath, address);
            }

            return address.Value;
        }
        public ChatCommandsHandler(Func<string, CancellationToken, UniTask> changeRealmAsync)
        {
            this.changeRealmAsync = changeRealmAsync;
        }

        public bool TryExecuteCommand(in string message)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            if (IsWorldCommand(message))
            {
                changeRealmAsync(GetWorldAddress(message), CancellationToken.None).Forget();
                return true;
            }

            return false;
        }

        private static bool IsWorldCommand(in string message) =>
            (message.StartsWith(WORLD_COMMAND) || message.StartsWith(GOTO_COMMAND)) && message.EndsWith(WORLDS_SUFFIX);
    }
}
