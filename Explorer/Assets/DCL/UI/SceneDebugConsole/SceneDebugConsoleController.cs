using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleController : IDisposable
    {
        public delegate void ConsoleVisibilityChangedDelegate(bool isVisible);

        private readonly SceneDebugConsoleLogEntryBus logEntriesBus;
        private readonly SceneDebugConsoleLogHistory logsHistory;
        private readonly SceneDebugConsoleCommandsBus consoleCommandsBus;
        private readonly SceneDebugConsoleSettings consoleSettings;

        private UIDocument uiDocument;
        private VisualElement uiDocumentRoot;
        private VisualTreeAsset logEntryUXML;
        private ListView consoleListView;
        private bool isInputSelected;

        public event ConsoleVisibilityChangedDelegate ConsoleVisibilityChanged;

        public SceneDebugConsoleController(
            SceneDebugConsoleLogEntryBus logEntriesBus,
            SceneDebugConsoleLogHistory logsHistory,
            SceneDebugConsoleCommandsBus consoleCommandsBus,
            SceneDebugConsoleSettings consoleSettings)
        {
            this.logEntriesBus = logEntriesBus;
            this.logsHistory = logsHistory;
            this.consoleCommandsBus = consoleCommandsBus;
            this.consoleSettings = consoleSettings;

            InstantiateRootGO();
        }

        // Instantiate root UI Document GameObject
        private void InstantiateRootGO()
        {
            logEntriesBus.MessageAdded += OnEntryBusEntryAdded;
            logsHistory.LogMessageAdded += OnLogsHistoryEntryAdded;
            consoleCommandsBus.OnClearConsole += Clear;
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            uiDocument = Object.Instantiate(Resources.Load<GameObject>("SceneDebugConsoleRootCanvas")).GetComponent<UIDocument>();
            uiDocumentRoot = uiDocument.rootVisualElement;
            uiDocumentRoot.visible = false;

            logEntryUXML = Resources.Load<VisualTreeAsset>("SceneDebugConsoleLogEntry");
            consoleListView = uiDocumentRoot.Q<ListView>(name: "console");
            consoleListView.makeItem = () =>
            {
                // Instantiate the UXML for the entry
                var newEntry = logEntryUXML.Instantiate();

                // Instantiate a controller for the data
                // var newEntryLogic = new LogEntryController();

                // Assign the controller script to the visual element
                // newLogEntry.userData = newEntryLogic;

                // Initialize the controller script
                // newEntryLogic.SetVisualElement(newLogEntry);

                // Return the root of the instantiated visual tree
                return newEntry;
            };
            consoleListView.bindItem = (item, index) =>
            {
                // (item.userData as CharacterListEntryController)?.SetCharacterData(m_AllCharacters[index]);
                item.Q<Label>(className: "log-entry__label").text = logsHistory.LogMessages[index].Message;
            };
            // Set the actual item's source list/array
            consoleListView.itemsSource = logsHistory.LogMessages;

            consoleListView.fixedItemHeight = 45;

            // FOR DEBUGGING...
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

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
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

        private void Clear()
        {
            logsHistory.ClearLogMessages();
        }

        private void OnLogsHistoryEntryAdded(SceneDebugConsoleLogEntry logEntry)
        {
            Debug.Log($"PRAVS - Controller.OnLogsHistoryEntryAdded({logEntry.Message})");
        }

        /*private void OnViewFoldingChanged(bool isUnfolded)
        {
            ConsoleVisibilityChanged?.Invoke(isUnfolded);
        }*/

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
            uiDocumentRoot.visible = !uiDocumentRoot.visible;
        }

        // TODO: IS THE LOG MESSAGEBUS + HISTORY NEEDED? CAN WE HAVE ONLY 1 BUS ??
        private void OnEntryBusEntryAdded(SceneDebugConsoleLogEntry entry)
        {
            // consoleListView.ClearSelection();

            // var updated = new List<SceneDebugConsoleLogEntry>(logsHistory.LogMessages) { entry };
            logsHistory.AddLogMessage(entry);
            // consoleListView.itemsSource = updated;
            consoleListView.RefreshItems();
            // consoleListView.style.display = DisplayStyle.None;
            // consoleListView.style.display = DisplayStyle.Flex;

            // TODO: HOW TO MAKE THE LISTVIEW RE-DRAW??? IT ONLY HAPPENS WHEN I MODIFY THE VIEWPORT MANUALLY...
        }
    }
}
