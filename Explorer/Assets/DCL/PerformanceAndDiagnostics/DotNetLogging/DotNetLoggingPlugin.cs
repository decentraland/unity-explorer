using Common.Logging;
using Common.Logging.Simple;

namespace DCL.PerformanceAndDiagnostics.DotNetLogging
{
    public class DotNetLoggingPlugin
    {
        public static void Initialize()
        {
            // In some platforms when we use IPFS hashing functions, which internally uses the .NET LogManager,
            // it throws NotSupportedException: System.Configuration.ConfigurationManager::GetSection at System.Configuration.ConfigurationManager.GetSection
            // Seems to be missing the logging configuration
            // This will ignore all generated logs used through the LogManager. The rest of the logs, should keep working as expected
            LogManager.Adapter = new NoOpLoggerFactoryAdapter();
        }
    }
}