using CodeLess.Attributes;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utility;
using Sentry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics
{
    /// <summary>
    ///     Allows to reference Sentry transaction by a static string value
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class SentryTransactionNameMapping : SentryTransactionMapping<string>
    {
        public void StartSentryTransaction(TransactionData transactionData)
        {
            ITransactionTracer transactionTracer = SentryTransactionManager.Instance.StartSentryTransaction(new TransactionContext(transactionData.TransactionName, transactionData.TransactionOperation));

            if (transactionTracer.IsSampled is true)
            {
                if (transactionData.Tags != null)
                    transactionTracer.SetTags(transactionData.Tags);

                IEnumerable<(string, object)>? extra = transactionData.ExtraData;

                if (extra != null)
                {
                    foreach ((string key, object value) in extra)
                        transactionTracer.SetExtra(key, value);
                }
            }
        }
    }

    /// <summary>
    ///     Allows to reference Sentry transaction by a static <see cref="T" /> value
    /// </summary>
    /// <typeparam name="T">Uniquely identifies the underlying transaction</typeparam>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class SentryTransactionMapping<T>
    {
        private readonly ConcurrentDictionary<T, ITransactionTracer> sentryTransactions = new ();

        public bool TryGet(T key, out ITransactionTracer transactionTracer) =>
            sentryTransactions.TryGetValue(key, out transactionTracer);

        public ITransactionTracer? StartSentryTransaction(T key, TransactionContext context, IReadOnlyDictionary<string, object?>? customSamplingContext = null)
        {
            if (sentryTransactions.ContainsKey(key)) return null;

            ITransactionTracer transaction = SentryTransactionManager.Instance.StartSentryTransaction(context, customSamplingContext);

            sentryTransactions.TryAdd(key, transaction);
            return transaction;
        }

        public void StartSpan(T transactionKey, SpanData spanData)
        {
            if (!sentryTransactions.TryGetValue(transactionKey, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transactionKey}' not found");
                return;
            }

            SentryTransactionManager.Instance.StartSpan(transaction, spanData);
        }

        public void EndCurrentSpan(T transactionName)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer? transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transactionName}' not found");
                return;
            }

            SentryTransactionManager.Instance.EndCurrentSpan(transaction);
        }

        public void EndTransaction(T key, SpanStatus finishWithStatus = SpanStatus.Ok)
        {
            if (!sentryTransactions.TryRemove(key, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{key}' not found");
                return;
            }

            SentryTransactionManager.Instance.EndTransaction(transaction, finishWithStatus);
        }

        public void EndCurrentSpanWithError(T key, string errorMessage, Exception? exception = null)
        {
            if (!sentryTransactions.TryGetValue(key, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{key}' not found");
                return;
            }

            SentryTransactionManager.Instance.EndCurrentSpanWithError(transaction, errorMessage, exception);
        }

        public void EndTransactionWithError(T key, string errorMessage, SpanStatus spanStatus = SpanStatus.UnknownError, Exception? exception = null)
        {
            if (!sentryTransactions.TryRemove(key, out ITransactionTracer transaction))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{key}' not found");
                return;
            }

            SentryTransactionManager.Instance.EndTransactionWithError(transaction, errorMessage, spanStatus, exception);
        }
    }

    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class SentryTransactionManager
    {
        private const string ERROR_COUNT = "error_count";
        private const string ERROR_MESSAGE_TAG = "error.message";
        private const string ERROR_TYPE_TAG = "error.type";
        private const string ERROR_EXCEPTION_MESSAGE_TAG = "error.exception_message";
        private const string ERROR_STACK_TAG = "error.stack";

        private readonly ConcurrentDictionary<ITransactionTracer, int> sentryTransactionErrors = new ();
        private readonly ConcurrentDictionary<ITransactionTracer, Stack<ISpan>> transactionsSpans = new ();

        public SentryTransactionManager()
        {
            // Register for application lifecycle events to ensure transactions are finished
            ExitUtils.BeforeApplicationQuitting += OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        // Otherwise Sentry creates a new dictionary for every transaction
        private static readonly IReadOnlyDictionary<string, object?> NO_CUSTOM_SAMPLING_CONTEXT = new Dictionary<string, object?>();

        public ITransactionTracer StartSentryTransaction(TransactionContext context, IReadOnlyDictionary<string, object?>? customSamplingContext = null)
        {
            ITransactionTracer transactionTracer = SentrySdk.StartTransaction(context, customSamplingContext ?? NO_CUSTOM_SAMPLING_CONTEXT);

            // It doesn't make sense to store any fuzz if it wasn't sampled
            if (transactionTracer.IsSampled.GetValueOrDefault())
            {
                sentryTransactionErrors.TryAdd(transactionTracer, 0);
                transactionTracer.SetTag(ERROR_COUNT, "0");
            }

            return transactionTracer;
        }

        private static readonly ThreadSafeObjectPool<Stack<ISpan>> SPAN_STACK_POOL = new (() => new Stack<ISpan>(1), actionOnRelease: s => s.Clear(), collectionCheck: PoolConstants.CHECK_COLLECTIONS);

        public ISpan? StartSpan(ITransactionTracer transaction, SpanData spanData)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return null;

            if (!transactionsSpans.TryGetValue(transaction, out Stack<ISpan> spanStack))
            {
                spanStack = SPAN_STACK_POOL.Get();
                transactionsSpans[transaction] = spanStack;
            }

            if (spanStack.Count > 0 && spanData.Depth < spanStack.Count)
            {
                ISpan currentSpan = spanStack.Pop();
                currentSpan.Finish();
            }

            ISpan parentSpan = (spanStack.Count == spanData.Depth && spanStack.Count > 0) ? spanStack.Peek() : transaction;
            ISpan newSpan = parentSpan.StartChild(spanData.SpanOperation, spanData.SpanName);

            spanStack.Push(newSpan);

            return newSpan;
        }

        public void EndCurrentSpan(ITransactionTracer transaction)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return;

            if (!transactionsSpans.TryGetValue(transaction, out Stack<ISpan> spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No spans found for transaction '{transaction.Name}'");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No active spans to end for transaction '{transaction.Name}'");
                return;
            }

            ISpan currentSpan = spanStack.Pop();
            currentSpan.Finish();
        }

        public bool EndTransaction(ITransactionTracer transaction, SpanStatus finishWithStatus = SpanStatus.Ok)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return false;

            if (!transactionsSpans.TryRemove(transaction, out Stack<ISpan>? spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transaction.Name}' not found");
                return false;
            }

            // Inherit the errors from the last failed span if the status is not ok
            // it will enable grouping by errors on the level of transactions in Sentry

            if (finishWithStatus != SpanStatus.Ok)
            {
                ISpan? lastFailedSpan = transaction.Spans.FirstOrDefault(s => s.Status != SpanStatus.Ok);

                if (lastFailedSpan != null)
                {
                    CopyTag(lastFailedSpan, transaction, ERROR_MESSAGE_TAG);
                    CopyTag(lastFailedSpan, transaction, ERROR_TYPE_TAG);
                    CopyTag(lastFailedSpan, transaction, ERROR_EXCEPTION_MESSAGE_TAG);
                    CopyTag(lastFailedSpan, transaction, ERROR_STACK_TAG);
                }
            }

            while (spanStack.Count > 0)
            {
                ISpan span = spanStack.Pop();
                span.Finish(finishWithStatus);
            }

            SPAN_STACK_POOL.Release(spanStack);

            transaction.Finish(finishWithStatus);

            sentryTransactionErrors.TryRemove(transaction, out _);
            return true;
        }

        private static void CopyTag(ISpan from, ISpan to, string tag)
        {
            if (from.Tags.TryGetValue(tag, out string tagValue))
                to.SetTag(tag, tagValue);
        }

        public void EndTransactionWithError(ITransactionTracer transaction, string errorMessage, SpanStatus spanStatus = SpanStatus.UnknownError, Exception? exception = null)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return;

            if (!transactionsSpans.TryRemove(transaction, out Stack<ISpan>? spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"Transaction '{transaction.Name}' not found");
                return;
            }

            while (spanStack.Count > 0)
            {
                ISpan span = spanStack.Pop();
                span.Finish();
            }

            SPAN_STACK_POOL.Release(spanStack);

            // Set the transaction status to error and add error information
            transaction.SetTag("status", "error");
            transaction.SetTag(ERROR_MESSAGE_TAG, errorMessage);

            if (exception != null)
            {
                transaction.SetTag(ERROR_TYPE_TAG, exception.GetType().Name);
                transaction.SetTag(ERROR_STACK_TAG, exception.StackTrace);
                transaction.Finish(exception);
            }
            else { transaction.Finish(spanStatus); }

            sentryTransactionErrors.Remove(transaction, out _);
        }

        public void EndCurrentSpanWithError(ITransactionTracer transactionTracer, string errorMessage, Exception? exception = null)
        {
            if (sentryTransactionErrors.TryGetValue(transactionTracer, out int currentErrorCount))
            {
                int errorsCount = currentErrorCount + 1;
                sentryTransactionErrors[transactionTracer] = errorsCount;

                transactionTracer.SetTag(ERROR_COUNT, errorsCount.ToString());
            }

            if (!transactionsSpans.TryGetValue(transactionTracer, out Stack<ISpan> spanStack))
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No spans found for transaction '{transactionTracer.Name}'");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.Log(new ReportData(ReportCategory.ANALYTICS), $"No active spans to end for transaction '{transactionTracer.Name}'");
                return;
            }

            ISpan currentSpan = spanStack.Pop();

            currentSpan.SetTag("status", "error");
            currentSpan.SetTag(ERROR_MESSAGE_TAG, errorMessage);

            if (exception != null)
            {
                currentSpan.SetTag(ERROR_TYPE_TAG, exception.GetType().Name);
                currentSpan.SetTag(ERROR_EXCEPTION_MESSAGE_TAG, exception.Message);
                currentSpan.SetTag(ERROR_STACK_TAG, exception.StackTrace);
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
            var transactionKeys = new List<ITransactionTracer>(transactionsSpans.Keys);

            foreach (ITransactionTracer transactionKey in transactionKeys)
            {
                try
                {
                    EndTransaction(transactionKey, SpanStatus.Aborted);
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
        public IEnumerable<KeyValuePair<string, string>>? Tags;
        public IEnumerable<(string, object)>? ExtraData;
    }

    public struct SpanData
    {
        public string SpanName;
        public string SpanOperation;
        public int Depth;
    }
}
