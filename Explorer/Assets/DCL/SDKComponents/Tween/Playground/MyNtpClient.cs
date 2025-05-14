using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.SDKComponents.Tween.Playground
{
    [Serializable]
    public class NtpSample
    {
        public string Server;
        public double OffsetMs;
        public double RoundTripMs;
        public string Timestamp;
    }

    public class MyNtpClient : MonoBehaviour
    {
        private const double MAX_RTT_MS = 150;   // far-away servers
        private const double SMEAR_THRESHOLD_MS = 200;  // >200 ms = highly likely leap-smear

        public List<NtpSample> Samples = new ();

        [Header("NTP settings")]
        [Tooltip("List several servers to improve robustness.")]
        [SerializeField] private string[] ntpServers =
        {
            // Note: don't merge classical UTC and Leap-smear such as "time.google.com" or "time.aws.com"

            // nearest pool (99 % classical UTC, i.e. without Leap-smear)
            "0.pool.ntp.org",
            "1.pool.ntp.org",
            "2.pool.ntp.org",
            "3.pool.ntp.org",

            // big anycast providers without Leap-smear
            "time.windows.com",
            "time.cloudflare.com",
            "time.facebook.com",
            "time.nist.gov", // official North America
        };

        [SerializeField] private int ntpPort = 123;
        [SerializeField] private  int timeoutMs = 3000;

        // public string ServerTime;
        // public double roundTripMs;
        public double finalOffsetMs;

        public long CurrentServerTimeMs => (long)(NtpUtils.UnixUtcNowMs() + finalOffsetMs);

        [ContextMenu("VVV")]
        private void Poll()
        {
            foreach (string server in ntpServers)
                PollServer(server);

            var cluster = Samples
                         .GroupBy(p => Math.Round(p.OffsetMs / SMEAR_THRESHOLD_MS)) // шаг 200 мс
                         .OrderByDescending(g => g.Count())                       // берем самый массовый
                         .FirstOrDefault();

            finalOffsetMs = cluster?.Average(p => p.OffsetMs)
                                   ?? Samples.OrderBy(p => p.RoundTripMs).First().OffsetMs;
        }
        private void PollServer(string server)
        {
            byte[] ntpData = NtpUtils.CreateNtpRequestBuffer();

            using (UdpClient udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                udp.Client.SendTimeout    = timeoutMs;

                IPAddress address = null;
                foreach (var adr in Dns.GetHostAddresses(server))
                {
                    if (adr.AddressFamily is AddressFamily.InterNetwork)
                    {
                        address = adr;
                        break;
                    }
                }

                if (address == null) return;

                IPEndPoint ipEndPoint = new IPEndPoint(address, ntpPort);

                double clientSendT1 = NtpUtils.UnixUtcNowMs();
                long   swTicksT1   = Stopwatch.GetTimestamp();
                NtpUtils.WriteTimestamp(ntpData, 40, NtpUtils.UnixMillisecondsToNtpTimestamp(clientSendT1));

                udp.Send(ntpData, ntpData.Length, ipEndPoint);
                ntpData = udp.Receive(ref ipEndPoint); // blocks (timeout‑guarded)

                long   swTicksT4 = Stopwatch.GetTimestamp();
                double t4DeltaMs = NtpUtils.StopwatchTicksToMilliseconds(swTicksT4 - swTicksT1);
                double clientReceiveT4 = clientSendT1 + t4DeltaMs;

                double serverReceiveT2 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(ntpData, 32));
                double serverSendT3 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(ntpData, 40));

                double wayIn  = serverReceiveT2 - clientSendT1;
                double wayOut = clientReceiveT4 - serverSendT3;
                var offsetMs     = (wayIn - wayOut) / 2.0; // θ
                var roundTripMs  = (wayIn + wayOut); // δ

                DateTime serverTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(clientReceiveT4 + offsetMs)).UtcDateTime;
                var ServerTime = serverTime.ToString("O");

                // 7.  Diagnostics
                Debug.Log($"Server time : {ServerTime}");
                Debug.Log($"Offset      : {offsetMs:F3}ms (server is {(offsetMs >= 0 ? "ahead" : "behind")})");
                Debug.Log($"Delay (RTT) : {roundTripMs:F3}ms");

                if(roundTripMs < MAX_RTT_MS)
                    Samples.Add(new NtpSample{ OffsetMs = offsetMs, Server = server, RoundTripMs = roundTripMs, Timestamp = ServerTime });
            }
        }
    }
}
