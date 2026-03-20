using System.Text;

namespace ExplorerAutomationTests.Common
{
    public static class Reporter
    {
        public static AltDriver AltDriver { get; set; }        public static void Log(string message, bool withScreenshot = false, bool calledFromRun = false)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";

            TestContext.Progress.WriteLine(formattedMessage);
            if (!calledFromRun)
            {
                AllureApi.Step(message);
            } 
            if (withScreenshot)
            {
                TakeScreenshot();
            }
        }
        public static void TakeScreenshot(string customName = null)
        {
            if (AltDriver == null)
            {
                Log("Cannot take screenshot: AltDriver not set", withScreenshot: false);
                return;
            }

            try
            {
                var projectDirectory = Directory.GetCurrentDirectory();
                var screenshotDirectory = Path.Combine(projectDirectory, "screenshots");

                // Create directory if it doesn't exist
                if (!Directory.Exists(screenshotDirectory))
                {
                    Directory.CreateDirectory(screenshotDirectory);
                }

                var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                var fileName = customName ?? $"screenshot_{timestamp}";
                var screenshotPath = Path.Combine(screenshotDirectory, $"{fileName}.png");

                AltDriver.GetPNGScreenshot(screenshotPath);
                AllureApi.Step($"Screenshot taken: {fileName}", () =>
                {
                    AllureApi.AddAttachment(name: fileName, content: File.ReadAllBytes(screenshotPath), type: "image/png");
                });
            }
            catch (Exception ex)
            {
                Log($"Failed to take screenshot: {ex.Message}", withScreenshot: false);
            }
        }

        public static void AttachFileToAllure(string filePath, string customName = null)
        {
            var fileName = customName ?? Path.GetFileNameWithoutExtension(filePath);
            
            AllureApi.Step($"Attach file: {fileName}", () =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        Log($"Cannot attach file: File not found at {filePath}", withScreenshot: false);
                        return;
                    }

                    var fileExtension = Path.GetExtension(filePath).ToLower();
                    
                    // Determine content type based on file extension
                    string contentType = fileExtension switch
                    {
                        ".txt" or ".log" => "text/plain",
                        ".json" => "application/json",
                        ".xml" => "application/xml",
                        ".html" => "text/html",
                        ".csv" => "text/csv",
                        _ => "application/octet-stream"
                    };

                    AllureApi.AddAttachment(name: fileName, content: File.ReadAllBytes(filePath), type: contentType);
                    Log($"File attached to Allure report: {fileName}", withScreenshot: false);
                }
                catch (Exception ex)
                {
                    Log($"Failed to attach file to Allure: {ex.Message}", withScreenshot: false);
                }
            });
        }
}
}
