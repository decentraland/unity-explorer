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
        public string ServerName;
        public double OffsetMs;
        public double RoundTripMs;
        public string ServerTime;
        public DateTime Timestamp;
    }

    public class MyNtpClient : MonoBehaviour, INtpTimeService
    {
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

        public double _mitigatedOffsetMs = 0;
        public bool _hasValidSamples = false;
        private DateTime _lastMitigationTime;
        private int maxSampleAge = 300;

        public bool IsSynced { get; private set; }
        public ulong ServerTimeMs => (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _mitigatedOffsetMs);

        private float _timeSinceLastUpdate;

        private void Start()
        {
            Poll();
        }
        [SerializeField] private float updateInterval = 60f; // Секунд между обновлениями

        private void Update()
        {
            _timeSinceLastUpdate += UnityEngine.Time.unscaledDeltaTime;
            if (_timeSinceLastUpdate >= updateInterval)
            {
                Samples.Clear();
                Poll();
                _timeSinceLastUpdate = 0;
            }
        }

        [ContextMenu("VVV")]
        private void Poll()
        {
            try
            {
                foreach (string server in ntpServers)
                    PollServer(server);

                MiniFilter();
                MitigateAndCalculateOffset();
            }
            catch
            {
                // ignored
            }
        }

        private void MiniFilter()
        {
            var cluster = Samples
                         .GroupBy(p => Math.Round(p.OffsetMs / SMEAR_THRESHOLD_MS)) // шаг 200 мс
                         .OrderByDescending(g => g.Count())                       // берем самый массовый
                         .FirstOrDefault();

            finalOffsetMs = cluster?.Average(p => p.OffsetMs)
                            ?? Samples.OrderBy(p => p.RoundTripMs).First().OffsetMs;
        }

        private void MitigateAndCalculateOffset()
        {
            // Remove old samples
            Samples.RemoveAll(s => (DateTime.UtcNow - s.Timestamp).TotalSeconds > maxSampleAge);

            if (Samples.Count < 3)
            {
                _hasValidSamples = false;
                return;
            }

            // Filter 1: Remove samples with excessive round-trip time
            double medianRtt = CalculateMedian(Samples.Select(s => s.RoundTripMs).ToList());
            var filteredByRtt = Samples
                               .Where(s => s.RoundTripMs < medianRtt * 2) // Filter out samples with RTT > 2x median
                               .ToList();

            if (filteredByRtt.Count < 3)
            {
                _hasValidSamples = false;
                return;
            }

            // Filter 2: Remove outlier offsets (samples far from the median)
            double medianOffset = CalculateMedian(filteredByRtt.Select(s => s.OffsetMs).ToList());
            var filteredByOffset = filteredByRtt
                .Where(s => Math.Abs(s.OffsetMs - medianOffset) < medianRtt) // Within 1 RTT of median offset
                .ToList();

            if (filteredByOffset.Count < 3)
            {
                _hasValidSamples = false;
                return;
            }

            // Select best 3 candidates (lowest RTT samples)
            var bestCandidates = filteredByOffset
                .OrderBy(s => s.RoundTripMs)
                .Take(3)
                .ToList();

            // Calculate weighted average offset (weight by inverse of RTT)
            double totalWeight = bestCandidates.Sum(s => 1.0 / s.RoundTripMs);
            double weightedOffsetSum = bestCandidates.Sum(s => s.OffsetMs * (1.0 / s.RoundTripMs));

            _mitigatedOffsetMs = weightedOffsetSum / totalWeight;
            _hasValidSamples = true;
            _lastMitigationTime = DateTime.UtcNow;
            IsSynced = true;

            Debug.Log($"Mitigated offset: {_mitigatedOffsetMs:F3}ms from {bestCandidates.Count} samples");
        }

        // Statistical helper: Calculate median of a list
        private static double CalculateMedian(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            var sortedValues = values.OrderBy(v => v).ToList();
            int count = sortedValues.Count;

            return count % 2 == 0
                ? (sortedValues[(count / 2) - 1] + sortedValues[count / 2]) / 2
                : sortedValues[count / 2];
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

                double clientSendT1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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

                Samples.Add(new NtpSample{ OffsetMs = offsetMs, ServerName = server, RoundTripMs = roundTripMs, ServerTime = serverTime.ToString("O"), Timestamp = DateTime.UtcNow});
            }
        }
    }
}
