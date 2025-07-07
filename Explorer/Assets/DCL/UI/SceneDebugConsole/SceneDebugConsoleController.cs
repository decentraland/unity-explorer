using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using DCL.Input;
using MVC;
using System;
using UnityEngine.InputSystem;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleController : IDisposable
    {
        public delegate void ConsoleVisibilityChangedDelegate(bool isVisible);

        private readonly SceneDebugConsoleLogEntryBus logEntriesBus;
        private readonly SceneDebugConsoleLogHistory logsHistory;
        private readonly IInputBlock inputBlock;
        private readonly SceneDebugConsoleCommandsBus consoleCommandsBus;
        private readonly SceneDebugConsoleSettings consoleSettings;

        private bool isInputSelected;

        public CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public bool IsUnfolded;
        /*{
            get => viewInstance != null && viewInstance.IsUnfolded;
            set
            {
                if (viewInstance != null)
                {
                    viewInstance.IsUnfolded = value;

                    if (value)
                        DCLInput.Instance.UI.Submit.performed += OnSubmitShortcutPerformed;
                    else
                        DCLInput.Instance.UI.Submit.performed -= OnSubmitShortcutPerformed;
                }
            }
        }*/

        public event ConsoleVisibilityChangedDelegate ConsoleVisibilityChanged;

        public SceneDebugConsoleController(
            SceneDebugConsoleLogEntryBus logEntriesBus,
            SceneDebugConsoleLogHistory logsHistory,
            IInputBlock inputBlock,
            SceneDebugConsoleCommandsBus consoleCommandsBus,
            SceneDebugConsoleSettings consoleSettings)
        {
            this.logEntriesBus = logEntriesBus;
            this.logsHistory = logsHistory;
            this.inputBlock = inputBlock;
            this.consoleCommandsBus = consoleCommandsBus;
            this.consoleSettings = consoleSettings;

            // TODO: bind to real view
            OnViewInstantiated();
        }

        public void Clear() // Called by a command
        {
            logsHistory.ClearLogMessages();
        }

        public void Dispose()
        {
            logEntriesBus.MessageAdded -= OnEntryBusEntryAdded;
            logsHistory.LogMessageAdded -= OnLogsHistoryEntryAdded;
            consoleCommandsBus.OnClearConsole -= Clear;

            /*if (viewInstance != null)
            {
                viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
                viewInstance.InputSubmitted -= OnViewInputSubmitted;
                viewInstance.FoldingChanged -= OnViewFoldingChanged;
                viewInstance.Dispose();
            }*/

            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            // DCLInput.Instance.UI.Submit.performed -= OnSubmitShortcutPerformed;
        }

        protected void OnViewInstantiated()
        {
            logEntriesBus.MessageAdded += OnEntryBusEntryAdded;
            logsHistory.LogMessageAdded += OnLogsHistoryEntryAdded;
            consoleCommandsBus.OnClearConsole += Clear;

            // viewInstance!.Initialize(logsHistory.LogMessages, consoleSettings);

            // Intro message
            // logEntriesBus.Send("1. Type '/help' for available commands.", LogType.Log);
            // logEntriesBus.Send("2. TEST ERROR LOG!!!", LogType.Error);
            // logEntriesBus.Send( "3. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            // logEntriesBus.Send("4. TEST LOG!!!", LogType.Log);
            // logEntriesBus.Send( "5. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
            // logEntriesBus.Send( "6. TEST ERROR LOG!!!", LogType.Error);
            // logEntriesBus.Send( "7. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            // logEntriesBus.Send("8. TEST LOG!!!", LogType.Log);
            // logEntriesBus.Send( "9. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
            // logEntriesBus.Send( "10. TEST WARNING LOG!!!", LogType.Warning);
            // logEntriesBus.Send( "11. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            // logEntriesBus.Send("12. TEST LOG!!!", LogType.Log);
            // logEntriesBus.Send( "13. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
        }

        private void OnLogsHistoryEntryAdded(SceneDebugConsoleLogEntry logEntry)
        {
            UnityEngine.Debug.Log($"PRAVS - Controller.OnLogsHistoryEntryAdded({logEntry.Message})");
            // viewInstance?.OnLogHistoryEntryAdded();
        }

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            ConsoleVisibilityChanged?.Invoke(isUnfolded);
        }

        protected void OnViewShow()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            IsUnfolded = false; // Start hidden by default
        }

        protected void OnViewClose()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
        }

        /*private void DisableUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private void EnableUnwantedInputs()
        {
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private void OnViewInputBoxFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }*/

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            IsUnfolded = !IsUnfolded;
        }

        // TODO: IS THE LOG MESSAGEBUS + HISTORY NEEDED? CAN WE HAVE ONLY 1 BUS ??
        private void OnEntryBusEntryAdded(SceneDebugConsoleLogEntry entry)
        {
            logsHistory.AddLogMessage(entry);
        }
    }
}
