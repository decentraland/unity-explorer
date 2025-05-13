using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Playground
{
    [ExecuteInEditMode]
    public class NTPClient : MonoBehaviour
    {
        [SerializeField] private string ntpServer = "pool.ntp.org"; // "time.google.com"
        [SerializeField] private int ntpPort = 123;
        [SerializeField] private float updateInterval = 60f; // Seconds between updates

        private DateTime _systemStartTime;
        private DateTime _lastNTPTime;
        private double _offsetFromNTP;
        private float _timeSinceLastUpdate;

        public DateTime CurrentNetworkTime => DateTime.UtcNow.AddSeconds(_offsetFromNTP);

        private void Start()
        {
            _systemStartTime = DateTime.UtcNow;
            SyncWithNTPServer();
        }

        private void Update()
        {
            _timeSinceLastUpdate += Time.deltaTime;
            if (_timeSinceLastUpdate >= updateInterval)
            {
                SyncWithNTPServer();
                _timeSinceLastUpdate = 0;
            }
        }

        private void SyncWithNTPServer()
        {
            try
            {
                var ntpData = new byte[48]; // NTP message size is 48 bytes

                // Set protocol version = 3 in LI_VN_MODE
                ntpData[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (client)

                // Create UDP client and connect to NTP server
                using (UdpClient client = new UdpClient())
                {
                    client.Client.ReceiveTimeout = 3000; // 3 seconds timeout

                    // Send request
                    IPEndPoint ipEndPoint = new IPEndPoint(Dns.GetHostEntry(ntpServer).AddressList[0], ntpPort);
                    client.Send(ntpData, ntpData.Length, ipEndPoint);

                    // Receive response
                    ntpData = client.Receive(ref ipEndPoint);
                }

                // Extract transmit timestamp (64-bit value, 8 bytes, at offset 40)
                ulong intPart = 0;
                ulong fractPart = 0;

                for (int i = 0; i < 4; i++)
                    intPart = (intPart << 8) | ntpData[40 + i];

                for (int i = 0; i < 4; i++)
                    fractPart = (fractPart << 8) | ntpData[44 + i];

                // Convert from NTP epoch (1900-01-01) to DateTime epoch (0001-01-01)
                ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                // NTP epoch starts Jan 1st 1900, subtract 70 years to get to Unix epoch
                TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);
                DateTime ntpTime = new DateTime(1900, 1, 1).Add(timeSpan);

                // Calculate offset
                _lastNTPTime = ntpTime;
                _offsetFromNTP = (_lastNTPTime - DateTime.UtcNow).TotalSeconds;

                Debug.Log($"NTP sync complete. Current network time: {CurrentNetworkTime} offset: {_offsetFromNTP}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"NTP sync failed: {ex.Message}");
            }
        }
    }
}
