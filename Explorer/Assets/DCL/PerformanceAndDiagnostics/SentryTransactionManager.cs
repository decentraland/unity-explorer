using CodeLess.Attributes;
using DCL.Diagnostics;
using DCL.Utility;
using Sentry;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics
{
    [Singleton]
    public partial class SentryTransactionManager
    {
        private readonly Dictionary<string, ITransactionTracer> sentryTransactions = new();
        private readonly Dictionary<string, int> sentryTransactionErrors = new();
        private readonly Dictionary<string, Stack<ISpan>> transactionsSpans = new();

        public SentryTransactionManager()
        {
            // Register for application lifecycle events to ensure transactions are finished
            ExitUtils.BeforeApplicationQuitting += OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        public void StartSentryTransaction(TransactionData transactionData)
        {
            if (sentryTransactions.ContainsKey(transactionData.TransactionName)) return;

            ITransactionTracer transactionTracer = SentrySdk.StartTransaction(transactionData.TransactionName, transactionData.TransactionOperation);

            transactionTracer.SetTag(transactionData.TransactionTag, transactionData.TransactionTagValue);

            sentryTransactions.Add(transactionData.TransactionName, transactionTracer);

            sentryTransactionErrors.Add(transactionData.TransactionName, 0);
            transactionTracer.SetExtra("error_count", 0);
        }

        public void StartSpan(SpanData spanData)
        {
            if (!sentryTransactions.TryGetValue(spanData.TransactionName, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{spanData.TransactionName}' not found");
                return;
            }

            if (!transactionsSpans.TryGetValue(spanData.TransactionName, out Stack<ISpan> spanStack))
            {
                spanStack = new Stack<ISpan>();
                transactionsSpans[spanData.TransactionName] = spanStack;
            }

            if (spanStack.Count > 0 && spanData.Depth < spanStack.Count)
            {
                ISpan currentSpan = spanStack.Pop();
                currentSpan.Finish();
            }

            ISpan parentSpan = (spanStack.Count == spanData.Depth && spanStack.Count > 0) ? spanStack.Peek() : transaction;
            ISpan newSpan = parentSpan.StartChild(spanData.SpanOperation, spanData.SpanName);

            spanStack.Push(newSpan);
        }

        public void EndCurrentSpan(string transactionName)
        {
            if (!transactionsSpans.TryGetValue(transactionName, out Stack<ISpan> spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No spans found for transaction '{transactionName}'");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No active spans to end for transaction '{transactionName}'");
                return;
            }

            ISpan currentSpan = spanStack.Pop();
            currentSpan.Finish();
        }

        public void EndTransaction(string transactionName)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transactionName}' not found");
                return;
            }

            if (transactionsSpans.TryGetValue(transactionName, out Stack<ISpan> spanStack))
            {
                while (spanStack.Count > 0)
                {
                    ISpan span = spanStack.Pop();
                    span.Finish();
                }
            }

            transaction.Finish();

            sentryTransactions.Remove(transactionName);
            transactionsSpans.Remove(transactionName);
        }

        public void EndTransactionWithError(string transactionName, string errorMessage, Exception? exception = null)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transactionName}' not found");
                return;
            }

            if (transactionsSpans.TryGetValue(transactionName, out Stack<ISpan> spanStack))
            {
                while (spanStack.Count > 0)
                {
                    ISpan span = spanStack.Pop();
                    span.Finish();
                }
            }

            // Set the transaction status to error and add error information
            transaction.SetTag("status", "error");
            transaction.SetTag("error.message", errorMessage);

            if (exception != null)
            {
                transaction.SetTag("error.type", exception.GetType().Name);
                transaction.SetTag("error.stack", exception.StackTrace);
                transaction.Finish(exception, SpanStatus.InternalError);
            }
            else
            {
                transaction.Finish(SpanStatus.UnknownError);
            }


            sentryTransactions.Remove(transactionName);
            transactionsSpans.Remove(transactionName);
        }

        public void EndCurrentSpanWithError(string transactionName, string errorMessage, Exception? exception = null)
        {
            if (sentryTransactionErrors.TryGetValue(transactionName, out int currentErrorCount))
            {
                sentryTransactionErrors[transactionName] = currentErrorCount + 1;

                if(sentryTransactions.TryGetValue(transactionName, out ITransactionTracer transaction))
                    transaction.SetExtra("error_count", sentryTransactionErrors[transactionName]);
            }

            if (!transactionsSpans.TryGetValue(transactionName, out Stack<ISpan> spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No spans found for transaction '{transactionName}'");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No active spans to end for transaction '{transactionName}'");
                return;
            }

            ISpan currentSpan = spanStack.Pop();

            currentSpan.SetTag("status", "error");
            currentSpan.SetTag("error.message", errorMessage);

            if (exception != null)
            {
                currentSpan.SetTag("error.type", exception.GetType().Name);
                currentSpan.SetTag("error.stack", exception.StackTrace);
                currentSpan.Finish(exception, SpanStatus.InternalError);
            }
            else
            {
                currentSpan.Finish(SpanStatus.UnknownError);
            }
        }

        private void OnApplicationQuitting()
        {
            FinishAllTransactions();
        }

        private void FinishAllTransactions()
        {
            var transactionKeys = new List<string>(sentryTransactions.Keys);

            foreach (string transactionKey in transactionKeys)
            {
                try
                {
                    EndTransaction(transactionKey);
                }
                catch (Exception ex)
                {
                    ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Error finishing transaction '{transactionKey}' during application quit: {ex.Message}");
                }
            }
        }
    }

    public struct TransactionData
    {
        public string TransactionName;
        public string TransactionOperation;
        public string TransactionTag;
        public string TransactionTagValue;
    }

    public struct SpanData
    {
        public string TransactionName;
        public string SpanName;
        public string SpanOperation;
        public int Depth;
    }
}
