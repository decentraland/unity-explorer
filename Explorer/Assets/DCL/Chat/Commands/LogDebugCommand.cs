using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class LogDebugChatCommand : IChatCommand
    {
        public string Command => "logDebug";
        public string Description => "<b>/logDebug </b>\n triggers test exceptions";

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
           const int numberOfExceptions = 100;
            // You can pass a context object if relevant, e.g., this command's GameObject if it had one.
            UnityEngine.Object contextObject = null;

            Debug.Log($"Starting to trigger {numberOfExceptions} exceptions for logging test...");

            for (int i = 0; i < numberOfExceptions; i++)
            {
                // Check for cancellation if the command execution can be cancelled
                if (ct.IsCancellationRequested)
                {
                    string cancellationMessage = $"Exception generation loop cancelled by CancellationToken after {i} exceptions.";
                    return UniTask.FromResult(cancellationMessage);
                }

                try
                {
                    OuterMethod(i);
                }
                catch (Exception ex)
                {
                    var data = new ReportData(ReportCategory.UNSPECIFIED,
                        ReportHint.None,
                        new SceneShortInfo(new Vector2Int(0, 0), "TestScene"));
                    
                    ReportHub.LogException(ex, data);
                }
            }

            string resultMessage = $"Successfully triggered and logged {numberOfExceptions} exceptions with category '{ReportCategory.UNSPECIFIED}'. " +
                                   $"Check Unity Console and ZLogger file log (if in standalone build).";

            return UniTask.FromResult(resultMessage);
        }
        
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void OuterMethod(int iteration)
        {
            InnerMethod(iteration);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void InnerMethod(int iteration)
        {
            // This will cause the DivideByZeroException
            PerformDivisionByZero(iteration);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void PerformDivisionByZero(int iterationValue)
        {
            int numerator = 10 + iterationValue; // Just to vary it slightly
            int denominator = 0;

            // This line will throw System.DivideByZeroException
            int result = numerator / denominator;

            // This line will never be reached, but shows intent
            Debug.Log($"Result of division: {result}");
        }
    }
}