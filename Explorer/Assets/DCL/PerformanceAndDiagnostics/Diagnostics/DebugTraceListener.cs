using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Unity does not provide a default implementation for TraceListener so all Assertions and Logs from <see cref="System.Diagnostics" /> are ignored
    /// </summary>
    public class DebugTraceListener : TraceListener
    {
#if DEBUG_ARCH && !UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void InitializeInBuild()
        {
            Inject();
        }
#endif

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        public static void InitializeInEditor()
        {
            Inject();
        }
#endif

        private static void Inject()
        {
            var traceInternal = Type.GetType("System.Diagnostics.TraceInternal,System");
            var listeners = (TraceListenerCollection)traceInternal.GetProperty("Listeners", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            listeners.Add(new DebugTraceListener());
        }

        public override void Fail(string message, string? detailMessage)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Diagnostics Fail: ");
            stringBuilder.Append(message);

            if (detailMessage != null)
            {
                stringBuilder.Append(" ");
                stringBuilder.Append(detailMessage);
            }

            throw new DiagnosticsException(stringBuilder.ToString());
        }

        public override void Write(string? message)
        {
            ReportHub.Log(ReportCategory.PLUGINS, message);
        }

        public override void WriteLine(string? message)
        {
            ReportHub.Log(ReportCategory.PLUGINS, message);
        }

        /// <summary>
        ///     Exception coming from System.Diagnostics
        /// </summary>
        public class DiagnosticsException : Exception, IDecentralandException
        {
            private readonly ReportData reportData;
            private string messagePrefix;

            public ref readonly ReportData ReportData => ref reportData;

            public override string Message => messagePrefix + base.Message;

            string IDecentralandException.MessagePrefix
            {
                set => messagePrefix = value;
            }

            internal DiagnosticsException(string message) : base(message)
            {
                reportData = new ReportData(ReportCategory.PLUGINS);
            }
        }
    }
}
