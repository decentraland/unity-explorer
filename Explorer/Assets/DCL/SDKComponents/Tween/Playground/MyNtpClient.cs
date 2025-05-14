using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

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
        public double pathInMs;     // client → server   (after clock‑base correction)
        public double pathOutMs;    // server → client   (after clock‑base correction)
        public double offsetMs;
        public double roundTripMs;

        [ContextMenu("VVV")]
        private void Poll()
        {
            byte[] ntpData = NtpUtils.CreateNtpClientModeRequestArray();

            using (UdpClient client = new UdpClient())
            {
                client.Client.ReceiveTimeout = timeoutMs;

                // Resolve first IPv4 address to avoid potential IPv6/UDP issues in some stacks
                // IPAddress address = (await Dns.GetHostAddressesAsync(server)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                //  IPAddress[] addrs = await Dns.GetHostAddressesAsync(ntpServers[0]);
                IPAddress[] addresses = Dns.GetHostAddresses(ntpServers[0]); // IPAddress[] addresses = Dns.GetHostEntry(ntpServers[0]).AddressList;
                IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], ntpPort); // Retrieve the IP addresses associated with the specified NTP server

                double clientSendT1 = NtpUtils.UnixUtcNowMs();
                NtpUtils.WriteTimestamp(ntpData, 40, NtpUtils.UnixMillisecondsToNtpTimestamp(clientSendT1));

                client.Send(ntpData, ntpData.Length, ipEndPoint);
                ntpData = client.Receive(ref ipEndPoint); // blocks (timeout‑guarded)
                double clientReceiveT4 = NtpUtils.UnixUtcNowMs();

                double serverReceiveT2 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(ntpData, 32));
                double serverSendT3 = NtpUtils.NtpEpochToUnixMilliseconds(NtpUtils.ReadTimestamp(ntpData, 40));

                double wayIn  = serverReceiveT2 - clientSendT1;
                double wayOut = clientReceiveT4 - serverSendT3;
                offsetMs     = (wayIn - wayOut) / 2.0; // θ
                roundTripMs  = (wayIn + wayOut); // δ

                pathInMs  = serverReceiveT2 - (clientSendT1 + offsetMs);
                pathOutMs = (clientReceiveT4 - offsetMs) - serverSendT3;

                DateTime serverTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(clientReceiveT4 + offsetMs)).UtcDateTime;
                ServerTime = serverTime.ToString($"{serverTime:O}");

                // 7.  Diagnostics
                Debug.Log($"Server time : {serverTime:O}");
                Debug.Log($"Offset      : {offsetMs:F3} ms (server is {(offsetMs >= 0 ? "ahead" : "behind")})");
                Debug.Log($"Delay (RTT) : {roundTripMs:F3} ms");
                Debug.Log($"Path ↑      : {pathInMs:F3} ms · Path ↓ : {pathOutMs:F3} ms");
            }
        }
    }
}
