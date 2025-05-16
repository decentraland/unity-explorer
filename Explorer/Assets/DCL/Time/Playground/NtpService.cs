using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Playground
{
    [ExecuteInEditMode]
    public class NtpService : MonoBehaviour
    {
        [SerializeField] private string ntpServer = "time.google.com"; // или pool.ntp.org
        [SerializeField] private int ntpPort = 123;
        [SerializeField] private float updateInterval = 60f; // Секунд между обновлениями
        [SerializeField] private int timeoutMs = 3000; // Таймаут в миллисекундах

        [Space]
        // Публичные данные
        public double OffsetMs; // Tclock = local + Offset
        public double RoundTripMs;
        public bool IsSynced;

        private float _timeSinceLastUpdate;

        public DateTime SyncNetworkTime => DateTime.UtcNow.AddMilliseconds(OffsetMs);

        private void Start()
        {
            SyncWithNTPServer();
        }

        private void Update()
        {
            _timeSinceLastUpdate += UnityEngine.Time.deltaTime;
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
                // NTP message size - 48 bytes
                byte[] ntpData = new byte[48];

                // Set protocol version - LI = 0, VN = 3, Mode = 3 (client)
                ntpData[0] = 0x1B;

                // Используем UdpClient для связи
                using (UdpClient client = new UdpClient())
                {
                    client.Client.ReceiveTimeout = timeoutMs;

                    // Получаем IP адрес сервера
                    IPAddress[] addresses = Dns.GetHostEntry(ntpServer).AddressList;
                    IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], ntpPort);

                    // Начинаем измерять время
                    Stopwatch sw = Stopwatch.StartNew();
                    double t1 = sw.Elapsed.TotalMilliseconds;

                    // Отправляем запрос
                    client.Send(ntpData, ntpData.Length, ipEndPoint);

                    // Получаем ответ
                    ntpData = client.Receive(ref ipEndPoint);

                    // Время получения ответа
                    double t4 = sw.Elapsed.TotalMilliseconds;
                    sw.Stop();

                    // Читаем метки времени сервера из пакета (RFC 5905)
                    // T2 = Receive Timestamp - когда сервер получил наш запрос
                    ulong t2 = 0;//ReadTimestamp(ntpData, 32);

                    // T3 = Transmit Timestamp - когда сервер отправил ответ
                    ulong t3 = 0;//ReadTimestamp(ntpData, 40);

                    // Переводим в миллисекунды
                    double t2ms = 0;//NtpToUnixMilliseconds(t2);
                    double t3ms = 0;//NtpToUnixMilliseconds(t3);

                    // Расчет по 4-точечному методу
                    RoundTripMs = (t4 - t1) - (t3ms - t2ms);
                    OffsetMs = ((t2ms - t1) + (t3ms - t4)) / 2.0;

                    IsSynced = true;

                    UnityEngine.Debug.Log($"NTP sync complete. Offset: {OffsetMs}ms, RTT: {RoundTripMs}ms | [{t1}, {t2ms},{t3ms}, {t4}]");
                }
            }
            catch (Exception ex)
            {
                IsSynced = false;
                UnityEngine.Debug.LogError($"NTP sync failed: {ex.Message}");
            }
        }


    }
}
