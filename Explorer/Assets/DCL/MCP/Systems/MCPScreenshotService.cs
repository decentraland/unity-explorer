using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using ECS.Abstract;
using System;
using System.Threading;

namespace DCL.MCP
{
    /// <summary>
    ///     Сервис для управления асинхронными операциями со скриншотами через MCP.
    ///     Предоставляет удобный API для запроса скриншотов и ожидания их готовности.
    /// </summary>
    public class MCPScreenshotService
    {
        private readonly World world;
        private SingleInstanceEntity? camera;

        private SingleInstanceEntity Camera => camera ??= world.CacheCamera();

        public MCPScreenshotService(World world)
        {
            this.world = world;
        }

        /// <summary>
        ///     Запрашивает скриншот и асинхронно ждет его готовности
        /// </summary>
        /// <param name="source">Источник запроса (для идентификации)</param>
        /// <param name="timeoutSeconds">Таймаут ожидания в секундах</param>
        /// <param name="ct">Токен отмены</param>
        /// <returns>Tuple с результатом: (успех, base64 thumbnail, сообщение об ошибке)</returns>
        public async UniTask<(bool success, string thumbnailBase64, string error)> RequestScreenshotAsync(
            string source,
            float timeoutSeconds = 5f,
            CancellationToken ct = default)
        {
            try
            {
                // Проверяем, открыта ли InWorldCamera
                if (!world.Has<InWorldCameraComponent>(Camera)) { return (false, null, "InWorldCamera is not active"); }

                // Запрашиваем скриншот
                world.Add(Camera, new TakeScreenshotRequest { Source = source });

                // Ждем готовности скриншота
                bool isReady = await WaitForScreenshotAsync(source, timeoutSeconds, ct);

                if (!isReady) { return (false, null, "Screenshot capture timeout"); }

                // Получаем thumbnail
                if (MCPScreenshotStorage.HasScreenshot() && MCPScreenshotStorage.GetLastSource() == source)
                {
                    string thumbnailBase64 = MCPScreenshotStorage.GetLastThumbnailBase64();
                    return (true, thumbnailBase64, null);
                }

                return (false, null, "Screenshot not found in storage");
            }
            catch (OperationCanceledException) { return (false, null, "Operation cancelled"); }
            catch (Exception e) { return (false, null, e.Message); }
        }

        /// <summary>
        ///     Асинхронно ждет готовности скриншота с указанным source
        /// </summary>
        private async UniTask<bool> WaitForScreenshotAsync(string source, float timeoutSeconds, CancellationToken ct)
        {
            var elapsedTime = 0f;

            while (elapsedTime < timeoutSeconds)
            {
                // Проверяем отмену
                if (ct.IsCancellationRequested)
                    return false;

                // Проверяем готовность скриншота
                if (MCPScreenshotStorage.HasScreenshot() && MCPScreenshotStorage.GetLastSource() == source)
                    return true;

                // Ждем следующий фрейм
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                elapsedTime += UnityEngine.Time.deltaTime;
            }

            return false; // Таймаут
        }

        /// <summary>
        ///     Проверяет, открыта ли InWorldCamera
        /// </summary>
        public bool IsInWorldCameraActive() =>
            world.Has<InWorldCameraComponent>(Camera);
    }
}
