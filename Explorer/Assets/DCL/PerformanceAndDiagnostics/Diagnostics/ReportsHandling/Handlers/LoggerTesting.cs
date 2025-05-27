using System;
using System.Diagnostics;
using System.Text;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using UnityEngine.Profiling;

namespace DCL.Diagnostics
{
    public class LoggerTesting : MonoBehaviour
    {
        private ReportHubLogger _logger;
        private Object _testContextObject;

        [Header("Benchmark Settings")]
        public int benchmarkIterations = 100000;
        public int warmupIterations = 100;
        
        [Header("String Operation Benchmark Data")]
        public string string1 = "Hello";
        public string string2 = "World";
        public string string3 = "from";
        public string string4 = "DCL!";
        public int intVal1 = 123;
        public float floatVal2 = 456.789f;
        public string formatString = "The quick {0} fox {1} over the lazy {2}. Values: {3}, {4:F2}";
        
        public void Initialize(ReportHubLogger logger)
        {
            _logger = logger;
        }

        void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
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
            
            // New key presses for string benchmarks
            if (keyboard.digit1Key.wasPressedThisFrame) BenchmarkStringConcatenation();
            if (keyboard.digit2Key.wasPressedThisFrame) BenchmarkStringFormatting();
            if (keyboard.digit3Key.wasPressedThisFrame) BenchmarkStringBuilderOperations();
        }
        
        [ContextMenu("Benchmark String Concatenation (Std vs ZString)")]
        public void BenchmarkStringConcatenation()
        {
            Debug.LogWarning("--- Starting String Concatenation Benchmark ---");
            
            int checksum = 0;
            for (int i = 0; i < warmupIterations; i++)
            {
                string temp1 = string1 + " " + string2 + " " + string3 + " " + string4;
                checksum += temp1.GetHashCode();
                
                using (var zsb = ZString.CreateStringBuilder())
                {
                    zsb.Append(string1);
                    zsb.Append(" ");
                    zsb.Append(string2);
                    zsb.Append(" ");
                    zsb.Append(string3);
                    zsb.Append(" ");
                    zsb.Append(string4);
                    string temp2 = zsb.ToString();
                    checksum += temp2.GetHashCode();
                }
            }

            Profiler.BeginSample("String Concatenation (+)");
            var swStdConcat = Stopwatch.StartNew();
            for (var i = 0; i < benchmarkIterations; i++)
            {
                string result = string1 + " " + string2 + " " + string3 + " " + string4;
                checksum += result.GetHashCode();
            }
            swStdConcat.Stop();
            Profiler.EndSample();
            
            LogBenchmarkResult("String Concat (+)", swStdConcat.ElapsedMilliseconds);

            Profiler.BeginSample("ZString.Concat");
            var swZStringConcat = Stopwatch.StartNew();
            for (var i = 0; i < benchmarkIterations; i++)
            {
                string result = ZString.Concat(string1, " ", string2, " ", string3, " ", string4);
                checksum += result.GetHashCode();
                if (result.Length == -1) Debug.Log("Never happens");
            }
            swZStringConcat.Stop();
            Profiler.EndSample();
            LogBenchmarkResult("ZString.Concat", swZStringConcat.ElapsedMilliseconds);
            Debug.LogWarning("--- Finished String Concatenation Benchmark ---" + checksum);
        }

        [ContextMenu("Benchmark String Formatting (Std vs ZString)")]
        public void BenchmarkStringFormatting()
        {
            var checksum = 0;
            Debug.LogWarning("--- Starting String Formatting Benchmark ---");
            string[] args = { "brown", "jumps", "dog"};
            
            for (var i = 0; i < warmupIterations; i++)
            {
                var t1 = args[0] + " " + args[1] + " " + args[2];
                var t1_1 = ZString.Concat(args[0], " ", args[1], " ", args[2]);
                var t2 = $"x:{args[0]} y:{args[1]} z:{args[2]}";
                var t2_1 = ZString.Format("x:{0} y:{1} z:{2}", args[0], args[1], args[2]);
                
                checksum += t1_1.GetHashCode();
                checksum += t1.GetHashCode();
                checksum += t2.GetHashCode();
                checksum += t2_1.GetHashCode();
            }

            Profiler.BeginSample("String Formatting");
            var swStdFormat = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                string result = $"x:{args[0]} y:{args[1]} z:{args[2]}";
                checksum += result.GetHashCode();
                if (result.Length == -1) Debug.Log("Never happens");
            }
            swStdFormat.Stop();
            Profiler.EndSample();
            LogBenchmarkResult("String.Format", swStdFormat.ElapsedMilliseconds);

            Profiler.BeginSample("ZString.Format");
            var swZStringFormat = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                string result = ZString.Format("x:{0} y:{1} z:{2}", args[0], args[1], args[2]);
                checksum += result.GetHashCode();
                if (result.Length == -1) Debug.Log("Never happens");
            }
            swZStringFormat.Stop();
            Profiler.EndSample();
            LogBenchmarkResult("ZString.Format", swZStringFormat.ElapsedMilliseconds);
            Debug.LogWarning("--- Finished String Formatting Benchmark ---" + checksum);
        }

        [ContextMenu("Benchmark StringBuilder (Std vs ZString)")]
        public void BenchmarkStringBuilderOperations()
        {
            int checksum = 0;
            Debug.LogWarning("--- Starting StringBuilder Benchmark ---");

            // Warm-up
            for (int i = 0; i < warmupIterations; i++)
            {
                var sb1 = new StringBuilder();
                sb1.Append(string1)
                    .Append(" ")
                    .Append(string2)
                    .Append(intVal1)
                    .Append(string3)
                    .AppendFormat("{0:F2}", floatVal2)
                    .Append(string4);
                
                
                string temp1 = sb1.ToString();
                checksum += temp1.GetHashCode();

                using (var zsb1 = ZString.CreateStringBuilder())
                {
                    zsb1.Append(string1);
                    zsb1.Append(" ");
                    zsb1.Append(string2);
                    zsb1.Append(intVal1);
                    zsb1.Append(string3);
                    zsb1.Append(floatVal2, "F2");
                    zsb1.Append(string4);
                    string temp2 = zsb1.ToString();
                    
                    checksum += temp2.GetHashCode();
                }
            }

            Profiler.BeginSample("String Builder");
            var swStdBuilder = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                var sb = new StringBuilder();
                sb.Append(string1);
                sb.Append(" ");
                sb.Append(string2);
                sb.Append(intVal1);
                sb.Append(string3);
                sb.AppendFormat("{0:F2}", floatVal2);
                sb.Append(string4);
                
                string result = sb.ToString();
                checksum += result.GetHashCode();
            }
            
            swStdBuilder.Stop();
            Profiler.EndSample();
            LogBenchmarkResult("System.Text.StringBuilder", swStdBuilder.ElapsedMilliseconds);

            // ZString.CreateStringBuilder
            UnityEngine.Profiling.Profiler.BeginSample("ZString.Builder");
            Stopwatch swZStringBuilder = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                using (var zsb = ZString.CreateStringBuilder()) // Using statement handles pooling
                {
                    zsb.Append(string1);
                    zsb.Append(" ");
                    zsb.Append(string2);
                    zsb.Append(intVal1);
                    zsb.Append(string3);
                    zsb.Append(floatVal2, "F2");
                    zsb.Append(string4);
                    
                    string result = zsb.ToString();
                    checksum += result.GetHashCode();
                }
            }
            swZStringBuilder.Stop();
            Profiler.EndSample();
            LogBenchmarkResult("ZString.CreateStringBuilder", swZStringBuilder.ElapsedMilliseconds);
            Debug.LogWarning("--- Finished StringBuilder Benchmark ---" + checksum);
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
                _logger.LogException(ex, errorData);
            }

            // IDecentralandException
            var dclEx = new DebugTraceListener.DiagnosticsException("diagnostics exception");
            _logger.LogException(dclEx); // Default: ReportHandler.All
            _logger.LogException(dclEx);

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
                _logger.Log(LogType.Log, benchData, message, _testContextObject);

            Profiler.BeginSample("Log (All Handlers)");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.Log(LogType.Log, benchData, message, _testContextObject);
            }
            sw.Stop();
            Profiler.EndSample();
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

            Profiler.BeginSample("LogFormat (Single Handler - Console)");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.LogFormat(LogType.Log, benchData, format, ReportHandler.All, args);
            }
            sw.Stop();
            Profiler.EndSample();
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

            Profiler.BeginSample("LogException (Standard, All Handlers)");
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
            Profiler.EndSample();
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

            Profiler.BeginSample("LogException (DCL, All Handlers)");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < benchmarkIterations; i++)
            {
                _logger.LogException(dclEx);
            }
            sw.Stop();
            Profiler.EndSample();
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
