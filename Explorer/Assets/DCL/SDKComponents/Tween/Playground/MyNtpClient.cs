using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.SDKComponents.Tween.Playground
{
    public class MyNtpClient : MonoBehaviour
    {
        [Header("NTP settings")]
        [Tooltip("List several servers to improve robustness.")]
        [SerializeField] private string[] ntpServers =
        {
            // nearest pool (99 % classical UTC, i.e. without Leap-smear)
            "0.pool.ntp.org",
            "1.pool.ntp.org",
            "2.pool.ntp.org",
            "3.pool.ntp.org",

            // big anycast providers without Leap-smear
            "time.cloudflare.com",
            "time.windows.com",
            "time.facebook.com",
            "time.nist.gov", // official North America

            // Don't merge classical UTC and Leap-smear
            // "time.google.com", // Leap-smear
            // "time.aws.com", //  leap-smear
        };

        [SerializeField] private int ntpPort = 123;
        [SerializeField] private  int timeoutMs = 3000;

        public string ServerTime;
        public double offsetMs;
        public double roundTripMs;

        [ContextMenu("VVV")]
        private void Poll()
        {
            byte[] ntpData = NtpUtils.CreateNtpRequestBuffer();

            using (UdpClient udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                udp.Client.SendTimeout    = timeoutMs;

                IPAddress[] addresses = Dns.GetHostAddresses(ntpServers[0]);
                IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], ntpPort);

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
                offsetMs     = (wayIn - wayOut) / 2.0; // θ
                roundTripMs  = (wayIn + wayOut); // δ

                DateTime serverTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(clientReceiveT4 + offsetMs)).UtcDateTime;
                ServerTime = serverTime.ToString("O");

                // 7.  Diagnostics
                Debug.Log($"Server time : {serverTime:O}");
                Debug.Log($"Offset      : {offsetMs:F3}ms (server is {(offsetMs >= 0 ? "ahead" : "behind")})");
                Debug.Log($"Delay (RTT) : {roundTripMs:F3}ms");
            }
        }
    }
}
