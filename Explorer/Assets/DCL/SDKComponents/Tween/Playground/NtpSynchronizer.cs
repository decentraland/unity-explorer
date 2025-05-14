using Cysharp.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.SDKComponents.Tween.Playground
{
    /// <summary>
    /// Кросс-платформенная синхронизация времени по NTP.
    /// Использует UniTask и четырёхточечный алгоритм θ/δ.
    /// </summary>
    [ExecuteInEditMode]
    public class NtpSynchronizer : MonoBehaviour
    {
        [Header("NTP settings")]
        public string ntpServer = "pool.ntp.org";
        public int ntpPort = 123;
        public int timeoutMs = 3000;
        public float updateInterval = 60f;          // сек. между повторными запросами

        [Header("Runtime state (read-only)")]
        public bool isSynced;
        public double offsetMs;                     // Ttrue = Tlocal + offsetMs
        public double roundTripMs;

        private CancellationTokenSource cts;
        private UdpClient udp;

        private void Start()
        {
            cts = new CancellationTokenSource();

            udp = new UdpClient();
            udp.Client.ReceiveTimeout = timeoutMs;
            udp.Connect(ntpServer, ntpPort);

            // PollLoop(cts.Token).Forget();      // fire-and-forget
        }

        private void OnDestroy() => cts?.Cancel();

        [ContextMenu("VVV")]
        private void Poll()
        {
            SyncOnce(cts.Token).Forget();
        }


        private async UniTaskVoid PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await SyncOnce(token);
                await UniTask.Delay(TimeSpan.FromSeconds(updateInterval), cancellationToken: token);
            }
        }

        private async UniTask SyncOnce(CancellationToken token)
        {
            try
            {
                var pkt = new byte[48];
                pkt[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

                double clientSendT1 = NtpUtils.UnixUtcNowMs();
                await udp.SendAsync(pkt, pkt.Length).AsUniTask();
                UdpReceiveResult response = await udp.ReceiveAsync().AsUniTask();
                double clientReceiveT4 = NtpUtils.UnixUtcNowMs();

                pkt = response.Buffer;
                double serverReceiveT2 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(pkt, 32));
                double serverSendT3 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(pkt, 40));

                roundTripMs = (clientReceiveT4 - clientSendT1) - (serverSendT3 - serverReceiveT2);
                offsetMs    = ((serverReceiveT2 - clientSendT1) + (serverSendT3 - clientReceiveT4)) / 2.0;
                isSynced    = true;

                Debug.Log($"VVV {serverReceiveT2} - {serverSendT3} | {clientReceiveT4} - {clientReceiveT4}");
            }
            catch (Exception ex)
            {
                isSynced = false;
                Debug.LogWarning($"[NTP] sync failed: {ex.Message}");
            }
        }


    }
}
