using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Typing;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    public class LogArchipelagoSignFlow : IArchipelagoSignFlow
    {
        private const string PREFIX = "LogArchipelagoSignFlow:";
        private readonly IArchipelagoSignFlow origin;
        private readonly Action<string> log;

        public LogArchipelagoSignFlow(IArchipelagoSignFlow origin) : this(
            origin,
            m => ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, m)
        ) { }

        public LogArchipelagoSignFlow(IArchipelagoSignFlow origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask ConnectAsync(string adapterUrl, CancellationToken token)
        {
            log($"{PREFIX} Connect start for {adapterUrl}");
            await origin.ConnectAsync(adapterUrl, token);
            log($"{PREFIX} Connect finish for {adapterUrl}");
        }

        public async UniTask<string> MessageForSignAsync(string ethereumAddress, CancellationToken token)
        {
            log($"{PREFIX} MessageForSignAsync start for address {ethereumAddress}");
            string result = await origin.MessageForSignAsync(ethereumAddress, token);
            log($"{PREFIX} MessageForSignAsync finish for address {ethereumAddress} with result {result}");
            return result;
        }

        public async UniTask<LightResult<string>> WelcomePeerIdAsync(string signedMessageAuthChainJson, CancellationToken token)
        {
            log($"{PREFIX} WelcomePeerIdAsync start for json {signedMessageAuthChainJson}");
            LightResult<string> result = await origin.WelcomePeerIdAsync(signedMessageAuthChainJson, token);
            log($"{PREFIX} WelcomePeerIdAsync finish for json {result} with result {result}");
            return result;
        }

        public async UniTask SendHeartbeatAsync(Vector3 playerPosition, CancellationToken token)
        {
            log($"{PREFIX} SendHeartbeatAsync start for position {playerPosition}");
            await origin.SendHeartbeatAsync(playerPosition, token);
            log($"{PREFIX} SendHeartbeatAsync finish for position {playerPosition}");
        }

        public UniTaskVoid StartListeningForConnectionStringAsync(Action<string> onNewConnectionString, CancellationToken token)
        {
            log($"{PREFIX} StartListeningForConnectionStringAsync start");

            return origin.StartListeningForConnectionStringAsync(newString =>
                {
                    log($"{PREFIX} StartListeningForConnectionStringAsync received string {newString}");
                    onNewConnectionString(newString);
                },
                token
            );
        }
    }
}
