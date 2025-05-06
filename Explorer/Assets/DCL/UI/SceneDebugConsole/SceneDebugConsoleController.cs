using Cysharp.Threading.Tasks;
using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using DCL.Input;
using DCL.Input.Component;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleController : ControllerBase<SceneDebugConsoleView>
    {
        public delegate void ConsoleVisibilityChangedDelegate(bool isVisible);

        private readonly SceneDebugConsoleMessageBus logMessagesBus;
        private readonly SceneDebugConsoleLogHistory logHistory;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly SceneDebugConsoleCommandsBus consoleCommandsBus;
        private readonly SceneDebugConsoleSettings consoleSettings;

        private bool isInputSelected;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public bool IsUnfolded
        {
            get => viewInstance != null && viewInstance.IsUnfolded;
            set
            {
                if (viewInstance != null)
                {
                    viewInstance.IsUnfolded = value;

                    if (value)
                        viewDependencies.DclInput.UI.Submit.performed += OnSubmitShortcutPerformed;
                    else
                        viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
                }
            }
        }

        public event ConsoleVisibilityChangedDelegate ConsoleVisibilityChanged;

        public SceneDebugConsoleController(
            ViewFactoryMethod viewFactory,
            SceneDebugConsoleMessageBus logMessagesBus,
            SceneDebugConsoleLogHistory logHistory,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            SceneDebugConsoleCommandsBus consoleCommandsBus,
            SceneDebugConsoleSettings consoleSettings) : base(viewFactory)
        {
            this.logMessagesBus = logMessagesBus;
            this.logHistory = logHistory;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.consoleCommandsBus = consoleCommandsBus;
            this.consoleSettings = consoleSettings;
        }

        public void Clear() // Called by a command
        {
            logHistory.ClearLogMessages();
            viewInstance?.RefreshLogs();
        }

        public override void Dispose()
        {
            logMessagesBus.MessageAdded -= OnMessageBusMessageAdded;
            logHistory.LogMessageAdded -= OnLogHistoryMessageAdded;
            consoleCommandsBus.OnClearConsole -= Clear;

            if (viewInstance != null)
            {
                viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
                viewInstance.InputSubmitted -= OnViewInputSubmitted;
                viewInstance.FoldingChanged -= OnViewFoldingChanged;
                viewInstance.Dispose();
            }

            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
        }

        protected override void OnViewInstantiated()
        {
            logMessagesBus.MessageAdded += OnMessageBusMessageAdded;
            logHistory.LogMessageAdded += OnLogHistoryMessageAdded;
            consoleCommandsBus.OnClearConsole += Clear;

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(logHistory.LogMessages, consoleSettings);

            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.FoldingChanged += OnViewFoldingChanged;

            OnFocus();

            // Intro message
            logMessagesBus.Send("1. Type '/help' for available commands.", LogType.Log);
            logMessagesBus.Send("2. TEST ERROR LOG!!!", LogType.Error);
            logMessagesBus.Send( "3. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            logMessagesBus.Send("4. TEST LOG!!!", LogType.Error);
            logMessagesBus.Send( "5. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
            logMessagesBus.Send( "6. TEST ERROR LOG!!!", LogType.Error);
            logMessagesBus.Send( "7. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            logMessagesBus.Send("8. TEST LOG!!!", LogType.Error);
            logMessagesBus.Send( "9. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
            logMessagesBus.Send( "10. TEST ERROR LOG!!!", LogType.Error);
            logMessagesBus.Send( "11. NullReferenceException: Object reference not set to an instance of an object\nDCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27)\nMVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36)\nDCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53)\nDCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31)\nDCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14)\nRethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal.\nUnityEngine.DebugLogHandler:LogException(Exception, Object)\nDCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109)\nDCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49)\nDCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126)\nDCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153)\nDCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15)\nGlobal.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94)\n", LogType.Error);
            logMessagesBus.Send("12. TEST LOG!!!", LogType.Error);
            logMessagesBus.Send( "13. NullReferenceException: Object reference not set to an instance of an object DCL.ApplicationBlocklistGuard.BlockedScreenController.Dispose () (at Assets/DCL/ApplicationBlocklistGuard/BlockedScreenController.cs:27) MVC.MVCManager.Dispose () (at Assets/DCL/Infrastructure/MVC/Manager/MVCManager.cs:36) DCL.PerformanceAndDiagnostics.Analytics.MVCManagerAnalyticsDecorator.Dispose () (at Assets/DCL/PerformanceAndDiagnostics/Analytics/DecoratorBased/MVCManagerAnalyticsDecorator.cs:53) DCL.PluginSystem.Global.MainUIPlugin.Dispose () (at Assets/DCL/PluginSystem/Global/MainUIPlugin.cs:31) DCL.Utilities.DisposableUtils.SafeDispose[T] (T disposable, DCL.Diagnostics.ReportData reportData, System.Func`2[T,TResult] exceptionMessageFactory) (at Assets/DCL/Utilities/DisposableUtils.cs:14) Rethrow as Exception: DCL.PluginSystem.Global.MainUIPlugin's thrown an exception on disposal. UnityEngine.DebugLogHandler:LogException(Exception, Object) DCL.Diagnostics.DebugLogReportHandler:LogExceptionInternal(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/DebugLogReportHandler.cs:109) DCL.Diagnostics.ReportHandlerBase:LogException(Exception, ReportData, Object) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/Handlers/ReportHandlerBase.cs:49) DCL.Diagnostics.ReportHubLogger:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHubLogger.cs:126) DCL.Diagnostics.ReportHub:LogException(Exception, ReportData, ReportHandler) (at Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportHub.cs:153) DCL.Utilities.DisposableUtils:SafeDispose(IDCLGlobalPlugin, ReportData, Func`2) (at Assets/DCL/Utilities/DisposableUtils.cs:15) Global.Dynamic.MainSceneLoader:OnDestroy() (at Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs:94) ", LogType.Error);
        }

        private void OnLogHistoryMessageAdded(SceneDebugConsoleLogMessage logMessage)
        {
            UnityEngine.Debug.Log($"PRAVS - Controller.OnLogHistoryMessageAdded({logMessage.Message})");
            viewInstance?.RefreshLogs();

            // if (consoleSettings.AutoScrollToBottom)
            //     viewInstance?.ShowLatestLogs();
        }

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            ConsoleVisibilityChanged?.Invoke(isUnfolded);
        }

        protected override void OnBlur()
        {
            viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            if (viewInstance!.IsFocused) return;

            viewInstance.EnableInputBoxSubmissions();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            IsUnfolded = false; // Start hidden by default
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.Never(ct);
        }

        private void DisableUnwantedInputs()
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
        }

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            IsUnfolded = !IsUnfolded;
        }

        private void OnSubmitShortcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBox();
        }

        private void OnViewInputSubmitted(string commandText)
        {
            /*if (string.IsNullOrWhiteSpace(commandText))
                return;

            // Log the command
            logMessagesBus.SendCommand(commandText);

            // Execute the command and get the response
            string response = consoleCommandsBus.ExecuteCommand(commandText);

            if (!string.IsNullOrEmpty(response))
            {
                // Log the response
                logMessagesBus.SendCommandResponse(response);
            }*/
        }

        // TODO: IS THE LOG MESSAGEBUS + HISTORY NEEDED? CAN WE HAVE ONLY 1 BUS ??
        private void OnMessageBusMessageAdded(SceneDebugConsoleLogMessage message)
        {
            logHistory.AddLogMessage(message);
        }
    }
}
