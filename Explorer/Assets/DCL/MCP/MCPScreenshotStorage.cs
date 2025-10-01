using UnityEngine;

namespace DCL.MCP
{
    /// <summary>
    ///     Простое хранилище для последнего созданного скриншота.
    ///     Используется MCPPlugin для передачи скриншотов через WebSocket.
    /// </summary>
    public static class MCPScreenshotStorage
    {
        private static Texture2D lastScreenshot;
        private static string lastSource = "";
        private static string lastThumbnailBase64;
        private const int THUMBNAIL_MAX_WIDTH = 800;
        private const int THUMBNAIL_JPEG_QUALITY = 75;

        /// <summary>
        ///     Сохраняет копию скриншота для последующей передачи через MCP
        /// </summary>
        public static void StoreScreenshot(Texture2D screenshot, string source)
        {
            if (screenshot == null) return;

            // Создаём копию текстуры чтобы избежать проблем с ownership
            if (lastScreenshot != null)
                Object.Destroy(lastScreenshot);

            lastScreenshot = Object.Instantiate(screenshot);
            lastSource = source;

            // Подготовим миниатюру (JPEG) для передачи в AI Host
            try { lastThumbnailBase64 = GenerateThumbnailBase64(lastScreenshot, THUMBNAIL_MAX_WIDTH, THUMBNAIL_JPEG_QUALITY); }
            catch
            {
                // Если не удалось — сбрасываем эскиз, но основной скрин всё равно доступен
                lastThumbnailBase64 = null;
            }
        }

        /// <summary>
        ///     Получает последний скриншот в формате PNG base64
        /// </summary>
        public static string GetLastScreenshotBase64()
        {
            if (lastScreenshot == null)
                return null;

            byte[] pngBytes = lastScreenshot.EncodeToPNG();
            return System.Convert.ToBase64String(pngBytes);
        }

        public static string GetLastThumbnailBase64() =>
            lastThumbnailBase64;

        /// <summary>
        ///     Получает source последнего скриншота
        /// </summary>
        public static string GetLastSource() =>
            lastSource;

        /// <summary>
        ///     Очищает хранилище
        /// </summary>
        public static void Clear()
        {
            if (lastScreenshot != null)
            {
                Object.Destroy(lastScreenshot);
                lastScreenshot = null;
            }

            lastSource = "";
            lastThumbnailBase64 = null;
        }

        /// <summary>
        ///     Проверяет, есть ли сохранённый скриншот
        /// </summary>
        public static bool HasScreenshot() =>
            lastScreenshot != null;

        private static string GenerateThumbnailBase64(Texture2D source, int maxWidth, int jpegQuality)
        {
            int width = source.width;
            int height = source.height;

            if (width <= maxWidth)
            {
                // Сжимаем только кодеком
                byte[] jpeg = source.EncodeToJPG(jpegQuality);
                return System.Convert.ToBase64String(jpeg);
            }

            float scale = (float)maxWidth / width;
            int targetW = Mathf.RoundToInt(width * scale);
            int targetH = Mathf.RoundToInt(height * scale);

            var rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var resized = new Texture2D(targetW, targetH, TextureFormat.RGB24, false);
            resized.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
            resized.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            byte[] jpg = resized.EncodeToJPG(jpegQuality);
            Object.Destroy(resized);
            return System.Convert.ToBase64String(jpg);
        }
    }
}
