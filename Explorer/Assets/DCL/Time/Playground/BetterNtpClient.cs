using DCL.SDKComponents.Tween.Playground;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.TimeSync
{
    public sealed class BetterNtpClient : MonoBehaviour
    {
        [Header("NTP settings")]
        [Tooltip("List several servers to improve robustness.")]
        [SerializeField] private string[] ntpServers =
        {
            "time.google.com",
            "pool.ntp.org",
            "time.windows.com"
        };

        [SerializeField] private float pollInterval = 60f;   // seconds between sync attempts
        [SerializeField] private int timeoutMs    = 3000;    // UDP receive timeout

        public bool   IsSynchronized { get; private set; }
        public double OffsetMs        { get; private set; }   // serverClock = localClock + OffsetMs
        public double RoundTripMs     { get; private set; }
        public DateTime NetworkTime   => DateTime.UtcNow.AddMilliseconds(OffsetMs);

        private CancellationTokenSource cts;

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            _ = PollLoopAsync(cts.Token);   // fire & forget
        }

        private void OnDisable()
        {
            cts.Cancel();
            cts.Dispose();
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await SyncOnceAsync(ct);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NTP] Sync attempt failed: {ex.Message}");
                    IsSynchronized = false;
                }

                try { await Task.Delay(TimeSpan.FromSeconds(pollInterval), ct); }
                catch (TaskCanceledException) { /* exiting */ }
            }
        }

        // One synchronization round; iterates over the list until success
        private async Task SyncOnceAsync(CancellationToken ct)
        {
            foreach (string server in ntpServers)
            {
                if (await QueryServerAsync(server, ct))
                    return; // success
            }

            throw new InvalidOperationException("All configured NTP servers are unreachable / produced invalid data.");
        }

        // Performs the four‑timestamp exchange with a single server
        // Returns true on success, false on timeout/network errors/invalid packet
        private async Task<bool> QueryServerAsync(string server, CancellationToken ct)
        {
            try
            {
                using (var udp = new UdpClient(AddressFamily.InterNetwork))
                {
                    udp.Client.ReceiveTimeout = timeoutMs;

                    // Resolve first IPv4 address to avoid potential IPv6/UDP issues in some stacks
                    IPAddress address = (await Dns.GetHostAddressesAsync(server))
                                           .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (address == null)
                        return false;

                    var endPoint = new IPEndPoint(address, 123);

                    // Build request (48‑byte zero‑filled array, first byte: LI=0 | VN=4 | Mode=3)
                    byte[] request = new byte[48];
                    request[0] = 0x23; // 0b0010_0011 : LI 0 | VN 4 | Mode 3

                    long t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await udp.SendAsync(request, request.Length, endPoint);

                    // Race: receive task vs timeout cancellation
                    Task<UdpReceiveResult> receiveTask = udp.ReceiveAsync();
                    Task timeoutTask = Task.Delay(timeoutMs, ct);
                    Task finished = await Task.WhenAny(receiveTask, timeoutTask);
                    if (finished != receiveTask)
                        return false; // timeout

                    long t3 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    byte[] data = receiveTask.Result.Buffer;
                    if (data.Length < 48)
                        return false; // malformed packet

                    ulong t1Raw = NtpUtils.ReadTimestamp(data, 32);
                    ulong t2Raw = NtpUtils.ReadTimestamp(data, 40);

                    double t1 = NtpUtils.NtpEpochToUnixMilliseconds(t1Raw);
                    double t2 = NtpUtils.NtpEpochToUnixMilliseconds(t2Raw);

                    double rtt   = (t3 - t0) - (t2 - t1);
                    double offset = ((t1 - t0) + (t2 - t3)) / 2.0;

                    if (rtt < 0 || Math.Abs(offset) > 60_000) // more than 1 min off -> probably bad sample
                        return false;

                    RoundTripMs     = rtt;
                    OffsetMs        = offset;
                    IsSynchronized  = true;

                    Debug.Log($"[NTP] {server} | Δ={offset:F2} ms  RTT={rtt:F2} ms");
                    return true;
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // swallow & report; caller will decide what to do
                Debug.LogWarning($"[NTP] Error querying {server}: {e.Message}");
                return false;
            }
        }


    }
}
