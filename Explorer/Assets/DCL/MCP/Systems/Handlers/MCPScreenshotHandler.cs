using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик команд для работы со скриншотами через MCP
    /// </summary>
    public class MCPScreenshotHandler
    {
        private readonly World world;
        private SingleInstanceEntity? camera;

        private SingleInstanceEntity Camera => camera ??= world.CacheCamera();

        public MCPScreenshotHandler(World world)
        {
            this.world = world;
        }

        /// <summary>
        ///     Делает скриншот (InWorldCamera должна быть открыта)
        /// </summary>
        public async UniTask<object> HandleTakeScreenshotAsync(JObject parameters)
        {
            string source = parameters["source"]?.ToString() ?? "MCP";

            // Проверяем, открыта ли InWorldCamera
            if (!world.Has<InWorldCameraComponent>(Camera))
            {
                return new
                {
                    success = false,
                    error = "InWorldCamera is not active. Please open it first using toggleInWorldCamera.",
                };
            }

            world.Add(Camera, new TakeScreenshotRequest { Source = source });

            ReportHub.Log(ReportCategory.DEBUG, $"[MCP Screenshot] Screenshot requested, source={source}");

            return new
            {
                success = true,
                source,
            };
        }

        /// <summary>
        ///     Возвращает последний скриншот в виде base64 PNG (если есть)
        /// </summary>
        public async UniTask<object> HandleGetLastScreenshotAsync(JObject parameters)
        {
            try
            {
                if (!MCPScreenshotStorage.HasScreenshot())
                {
                    return new
                    {
                        success = false,
                        error = "No screenshot available",
                    };
                }

                string base64 = MCPScreenshotStorage.GetLastScreenshotBase64();
                string thumbBase64 = MCPScreenshotStorage.GetLastThumbnailBase64();
                string source = MCPScreenshotStorage.GetLastSource();

                return new
                {
                    success = true,
                    imageBase64 = base64,
                    thumbnailBase64 = thumbBase64,
                    mimeType = "image/png",
                    source,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Screenshot] getLastScreenshot failed: {e.Message}");

                return new
                {
                    success = false,
                    error = e.Message,
                };
            }
        }

        /// <summary>
        ///     Возвращает подсказку где скачать последний скриншот (без передачи содержимого),
        ///     чтобы MCP мог скачать файл отдельно (например, через REST, если будет добавлено) или
        ///     просто вернуть base64 миниатюру, а полный — отдельным инструментом загрузки
        /// </summary>
        public async UniTask<object> HandleDownloadLastScreenshotAsync(JObject parameters)
        {
            if (!MCPScreenshotStorage.HasScreenshot())
            {
                return new
                {
                    success = false,
                    error = "No screenshot available",
                };
            }

            // Пока REST-скачивание не реализовано, возвращаем только то, что есть: миниатюру и информацию,
            // а MCP tool сможет сохранить base64 на диск
            string base64 = MCPScreenshotStorage.GetLastScreenshotBase64();
            string source = MCPScreenshotStorage.GetLastSource();

            return new
            {
                success = true,
                imageBase64 = base64,
                mimeType = "image/png",
                source,
                note = "For large files, consider saving base64 to disk on the MCP side.",
            };
        }
    }
}
