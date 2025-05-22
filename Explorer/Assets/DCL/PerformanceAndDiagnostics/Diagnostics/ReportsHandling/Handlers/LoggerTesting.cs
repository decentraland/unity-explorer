using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.Diagnostics
{
    public class LoggerTesting : MonoBehaviour
    {
        private ReportHubLogger _logger;
        private Object _testContextObject;

        [Header("Benchmark Settings")]
        public int benchmarkIterations = 10000;
        
        public void Initialize(ReportHubLogger logger)
        {
            _logger = logger;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current; // Get the current keyboard

            if (keyboard == null) // Important: Check if a keyboard is connected/available
                return;

            // Key presses with the new Input System
            if (keyboard.mKey.wasPressedThisFrame)
                ExecuteBasicLogTests();

            if (keyboard.nKey.wasPressedThisFrame)
                ExecuteLogFormatTests();

            if (keyboard.bKey.wasPressedThisFrame)
                ExecuteExceptionTests();

            if (keyboard.vKey.wasPressedThisFrame)
                ExecuteILogHandlerInterfaceTests();

            if (keyboard.cKey.wasPressedThisFrame)
                BenchmarkLogAllHandlers();

            if (keyboard.xKey.wasPressedThisFrame)
                BenchmarkLogFormatSingleHandler();

            if (keyboard.zKey.wasPressedThisFrame)
                BenchmarkLogExceptionStandard();

            if (keyboard.aKey.wasPressedThisFrame) // Assuming 'A' key for this
                BenchmarkLogDCLException();
        }
        
        // --- BASIC LOG TESTS ---
        [ContextMenu("1. Execute Basic Log Tests")]
        public void ExecuteBasicLogTests()
        {
            if (_logger == null) return;
            Debug.LogWarning("--- Starting Basic Log Tests ---");

            ReportData generalData = new ReportData(ReportCategory.UNSPECIFIED);

            _logger.Log(LogType.Log, generalData, $"[Log] Simple message {Time.time}", _testContextObject);
            _logger.Log(LogType.Warning, generalData, $"[Warning] With context {Time.time}", _testContextObject);
            _logger.Log(LogType.Error, new ReportData(ReportCategory.UNSPECIFIED), $"[Error] No context {Time.time}", null); // Test with null context

            // Test specific handler
            _logger.Log(LogType.Log, generalData, "[Log] Message FOR CONSOLE ONLY", _testContextObject);
            _logger.Log(LogType.Log, generalData, "[Log] Message FOR ANALYTICS ONLY", _testContextObject);

            Debug.LogWarning("--- Finished Basic Log Tests (Check Console) ---");
        }

        // --- LOGFORMAT TESTS ---
        [ContextMenu("2. Execute LogFormat Tests")]
        public void ExecuteLogFormatTests()
        {
            if (_logger == null) return;
            Debug.LogWarning("--- Starting LogFormat Tests ---");

            ReportData sceneData = new ReportData(ReportCategory.UNSPECIFIED);
            int val1 = Random.Range(1, 100);
            string val2 = "example_string";

            // Default (ReportHandler.All)
            _logger.LogFormat(LogType.Log, sceneData, "[LogFormat] All Handlers: Value1 = {0}, Value2 = '{1}' {2}", args: new object[] { val1, val2, Time.time });

            // Specific handler
            _logger.LogFormat(LogType.Warning, sceneData, "[LogFormat] CONSOLE ONLY: Value1 = {0} {1}", args: new object[] { val1, Time.time });
            _logger.LogFormat(LogType.Error, sceneData, "[LogFormat] ANALYTICS ONLY: Value2 = '{0}' {1}", args: new object[] { val2, Time.time });

            Debug.LogWarning("--- Finished LogFormat Tests (Check Console) ---");
        }

        // --- EXCEPTION TESTS ---
        [ContextMenu("3. Execute Exception Tests (do not test)")]
        public void ExecuteExceptionTests()
        {
            if (_logger == null) return;
            Debug.LogWarning("--- Starting Exception Tests ---");

            ReportData errorData = new ReportData(ReportCategory.UNSPECIFIED);

            // Standard Exception
            try { throw new ArgumentNullException("testParam", "This is a test ArgumentNullException."); }
            catch (ArgumentNullException ex)
            {
                _logger.LogException(ex, errorData); // Default: ReportHandler.All
                _logger.LogException(ex, errorData, ReportHandler.All);
            }

            // IDecentralandException
            var dclEx = new DebugTraceListener.DiagnosticsException("diagnostics exception");
            _logger.LogException(dclEx); // Default: ReportHandler.All
            _logger.LogException(dclEx, ReportHandler.All);

            // Test with an inner exception
             try
            {
                try { throw new System.IO.FileNotFoundException("Inner file not found."); }
                catch (Exception inner) { throw new DebugTraceListener.DiagnosticsException("Outer DCL error with inner."); }
            }
            catch (DebugTraceListener.DiagnosticsException exWithInner)
            {
                 _logger.LogException(exWithInner);
                 Debug.Log($"Logged DCL Exception with Inner: {exWithInner.InnerException?.Message}");
            }


            Debug.LogWarning("--- Finished Exception Tests (Check Console) ---");
        }

        // --- ILogHandler INTERFACE TESTS (used by Unity's Debug.Log redirection) ---
        [ContextMenu("4. Execute ILogHandler Interface Tests")]
        public void ExecuteILogHandlerInterfaceTests()
        {
            if (_logger == null) return;
            Debug.LogWarning("--- Starting ILogHandler Interface Tests ---");
            // To test this properly, you'd typically set Debug.unityLogger.logHandler = _logger;
            // For manual test, we call directly:
            ILogHandler iLogHandler = _logger;

            iLogHandler.LogFormat(LogType.Log, _testContextObject, "[ILogHandler.LogFormat] Message {0}, {1}", "param1", Time.frameCount);
            try { throw new InvalidTimeZoneException("Test ILogHandler Exception"); }
            catch (InvalidTimeZoneException ex)
            {
                iLogHandler.LogException(ex, _testContextObject);
            }
            Debug.LogWarning("--- Finished ILogHandler Interface Tests (Check Console) ---");
            Debug.LogWarning("NOTE: ILogHandler calls use ReportData.UNSPECIFIED internally.");
        }

        // --- BENCHMARKS ---
        [ContextMenu("Benchmark: Log (All Handlers)")]
        public void BenchmarkLogAllHandlers()
        {
            if (_logger == null) return;
            ReportData benchData = new ReportData(ReportCategory.UNSPECIFIED);
            string message = "Benchmark message";

            // Warm-up
            for(int i = 0; i < 100; i++)
                _logger.Log(LogType.Log, benchData, message, _testContextObject, ReportHandler.All);

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.Log(LogType.Log, benchData, message, _testContextObject, ReportHandler.All);
            }
            sw.Stop();
            LogBenchmarkResult("Log (All Handlers)", sw.ElapsedMilliseconds);
        }

        [ContextMenu("Benchmark: LogFormat (Single Handler)")]
        public void BenchmarkLogFormatSingleHandler()
        {
            if (_logger == null) return;
            ReportData benchData = new ReportData(ReportCategory.UNSPECIFIED);
            string format = "Benchmark format: {0}, {1}";
            object[] args = { 123, "test" };

             // Warm-up
            for(int i = 0; i < 100; i++)
                 _logger.LogFormat(LogType.Log, benchData, format, ReportHandler.All, args);

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.LogFormat(LogType.Log, benchData, format, ReportHandler.All, args);
            }
            sw.Stop();
            LogBenchmarkResult("LogFormat (Single Handler - Console)", sw.ElapsedMilliseconds);
        }

        [ContextMenu("Benchmark: LogException (Standard Exception, All Handlers)")]
        public void BenchmarkLogExceptionStandard()
        {
            if (_logger == null) return;
            ReportData benchData = new ReportData(ReportCategory.UNSPECIFIED);
            var ex = new DebugTraceListener.DiagnosticsException("diagnostics exception");

             // Warm-up
            for(int i = 0; i < 100; i++)
                _logger.LogException(ex, benchData);

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                // Creating new exception inside loop is more realistic if exceptions are frequently new
                // Exception exInstance = new ApplicationException("Benchmark exception iter " + i);
                // _logger.LogException(exInstance, benchData, ReportHandler.All);
                // For just measuring the logger part, reuse the exception:
                _logger.LogException(ex, benchData);
            }
            sw.Stop();
            LogBenchmarkResult("LogException (Standard, All Handlers)", sw.ElapsedMilliseconds);
        }
        
        [ContextMenu("Benchmark: LogException (DCL Exception, All Handlers)")]
        public void BenchmarkLogDCLException()
        {
            if (_logger == null) return;
            var dclEx = new DebugTraceListener.DiagnosticsException("diagnostics exception");

             // Warm-up
            for(int i = 0; i < 100; i++)
                 _logger.LogException(dclEx);

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.LogException(dclEx);
            }
            sw.Stop();
            LogBenchmarkResult("LogException (DCL, All Handlers)", sw.ElapsedMilliseconds);
        }
        
        private void LogBenchmarkResult(string benchmarkName, long milliseconds)
        {
            double msPerOp = (double)milliseconds / benchmarkIterations;
            double usPerOp = msPerOp * 1000.0;
            Debug.Log($"BENCHMARK RESULT - {benchmarkName}:\n" +
                      $"Total Iterations: {benchmarkIterations}\n" +
                      $"Total Time: {milliseconds} ms\n" +
                      $"Time per operation: {msPerOp:F4} ms ({usPerOp:F2} µs)");
        }
    }
    
    
}
