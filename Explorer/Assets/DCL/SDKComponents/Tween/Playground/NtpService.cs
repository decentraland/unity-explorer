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
                    ulong t2 = ReadTimestamp(ntpData, 32);

                    // T3 = Transmit Timestamp - когда сервер отправил ответ
                    ulong t3 = ReadTimestamp(ntpData, 40);

                    // Переводим в миллисекунды
                    double t2ms = NtpToUnixMilliseconds(t2);
                    double t3ms = NtpToUnixMilliseconds(t3);

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

        // Читаем 64-битную метку времени из буфера (big-endian)
        private static ulong ReadTimestamp(byte[] buffer, int offset)
        {
            ulong intPart = ((ulong)buffer[offset + 0] << 24) |
                            ((ulong)buffer[offset + 1] << 16) |
                            ((ulong)buffer[offset + 2] << 8) |
                            ((ulong)buffer[offset + 3]);

            ulong fracPart = ((ulong)buffer[offset + 4] << 24) |
                             ((ulong)buffer[offset + 5] << 16) |
                             ((ulong)buffer[offset + 6] << 8) |
                             ((ulong)buffer[offset + 7]);

            return (intPart << 32) | fracPart;
        }

        // Конвертация из NTP-времени в миллисекунды Unix-эпохи
        private static double NtpToUnixMilliseconds(ulong ntpTimestamp)
        {
            const ulong SecondsFrom1900To1970 = 2208988800UL;

            ulong seconds = ntpTimestamp >> 32;
            ulong fraction = ntpTimestamp & 0xFFFFFFFF;

            double milliseconds = (seconds - SecondsFrom1900To1970) * 1000.0 +
                                  (fraction * 1000.0 / 0x100000000L);

            return milliseconds;
        }
    }
}
