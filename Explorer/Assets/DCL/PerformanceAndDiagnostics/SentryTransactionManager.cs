using CodeLess.Attributes;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utility;
using Sentry;
using Sentry.Unity;
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
            ITransactionTracer? transactionTracer = StartSentryTransaction(
                transactionData.TransactionName,
                new TransactionContext(transactionData.TransactionName, transactionData.TransactionOperation));

            if (transactionTracer is { IsSampled: true })
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
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(StartSpan)}) Transaction '{transactionKey}' not found when starting span '{spanData.SpanName}' (op: {spanData.SpanOperation}, depth: {spanData.Depth})");
                return;
            }

            SentryTransactionManager.Instance.StartSpan(transaction, spanData);
        }

        public void EndCurrentSpan(T transactionName)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer? transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpan)}) Transaction '{transactionName}' not found");
                return;
            }

            SentryTransactionManager.Instance.EndCurrentSpan(transaction);
        }

        public void EndSpanOnDepth(T transactionName, int depth)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer? transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepth)}) Transaction '{transactionName}' not found (depth: {depth})");
                return;
            }

            SentryTransactionManager.Instance.EndSpanOnDepth(transaction, depth);
        }

        public void EndSpanOnDepthWithError(T transactionName, int depth, string errorMessage, Exception? exception = null)
        {
            if (!sentryTransactions.TryGetValue(transactionName, out ITransactionTracer? transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepthWithError)}) Transaction '{transactionName}' not found (depth: {depth}, error: {errorMessage})");
                return;
            }

            SentryTransactionManager.Instance.EndSpanOnDepthWithError(transaction, depth, errorMessage, exception);
        }

        public void EndTransaction(T key, SpanStatus finishWithStatus = SpanStatus.Ok)
        {
            if (!sentryTransactions.TryRemove(key, out ITransactionTracer transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndTransaction)}) Transaction '{key}' not found (status: {finishWithStatus})");
                return;
            }

            SentryTransactionManager.Instance.EndTransaction(transaction, finishWithStatus);
        }

        public void EndCurrentSpanWithError(T key, string errorMessage, Exception? exception = null)
        {
            if (!sentryTransactions.TryGetValue(key, out ITransactionTracer transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpanWithError)}) Transaction '{key}' not found (error: {errorMessage})");
                return;
            }

            SentryTransactionManager.Instance.EndCurrentSpanWithError(transaction, errorMessage, exception);
        }

        public void EndTransactionWithError(T key, string errorMessage, SpanStatus spanStatus = SpanStatus.UnknownError, Exception? exception = null)
        {
            if (!sentryTransactions.TryRemove(key, out ITransactionTracer transaction))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndTransactionWithError)}) Transaction '{key}' not found (error: {errorMessage})");
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
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpan)}) No span stack found for transaction '{transaction.Name}'");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpan)}) Span stack is empty for transaction '{transaction.Name}' — nothing to close");
                return;
            }

            ISpan currentSpan = spanStack.Pop();
            currentSpan.Finish();
        }

        public void EndSpanOnDepth(ITransactionTracer transaction, int depth)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return;

            if (depth < 1)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepth)}) Invalid depth '{depth}' for transaction '{transaction.Name}'. Depth must be >= 1");
                return;
            }

            if (!transactionsSpans.TryGetValue(transaction, out Stack<ISpan> spanStack))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepth)}) No span stack found for transaction '{transaction.Name}' (depth: {depth})");
                return;
            }

            while (spanStack.Count >= depth)
            {
                ISpan span = spanStack.Pop();
                span.Finish();
            }
        }

        public void EndSpanOnDepthWithError(ITransactionTracer transactionTracer, int depth, string errorMessage, Exception? exception = null)
        {
            if (!transactionTracer.IsSampled.GetValueOrDefault())
                return;

            if (depth < 1)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepthWithError)}) Invalid depth '{depth}' for transaction '{transactionTracer.Name}'. Depth must be >= 1");
                return;
            }

            if (!transactionsSpans.TryGetValue(transactionTracer, out Stack<ISpan> spanStack))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndSpanOnDepthWithError)}) No span stack found for transaction '{transactionTracer.Name}' (depth: {depth}, error: {errorMessage})");
                return;
            }

            int closedSpans = 0;

            while (spanStack.Count >= depth)
            {
                ISpan span = spanStack.Pop();
                FinishSpanWithError(span, errorMessage, exception);
                closedSpans++;
            }

            if (closedSpans > 0 && sentryTransactionErrors.TryGetValue(transactionTracer, out int currentErrorCount))
            {
                int errorsCount = currentErrorCount + closedSpans;
                sentryTransactionErrors[transactionTracer] = errorsCount;
                transactionTracer.SetTag(ERROR_COUNT, errorsCount.ToString());
            }
        }

        public bool EndTransaction(ITransactionTracer transaction, SpanStatus finishWithStatus = SpanStatus.Ok)
        {
            if (!transaction.IsSampled.GetValueOrDefault())
                return false;

            if (!transactionsSpans.TryRemove(transaction, out Stack<ISpan>? spanStack))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndTransaction)}) No span stack found for transaction '{transaction.Name}' (status: {finishWithStatus})");
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
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndTransactionWithError)}) No span stack found for transaction '{transaction.Name}' (error: {errorMessage})");
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
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpanWithError)}) No span stack found for transaction '{transactionTracer.Name}' (error: {errorMessage})");
                return;
            }

            if (spanStack.Count == 0)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(EndCurrentSpanWithError)}) Span stack is empty for transaction '{transactionTracer.Name}' — nothing to close (error: {errorMessage})");
                return;
            }

            ISpan currentSpan = spanStack.Pop();
            FinishSpanWithError(currentSpan, errorMessage, exception);
        }

        private static void FinishSpanWithError(ISpan span, string errorMessage, Exception? exception = null)
        {
            span.SetTag("status", "error");
            span.SetTag(ERROR_MESSAGE_TAG, errorMessage);

            if (exception != null)
            {
                span.SetTag(ERROR_TYPE_TAG, exception.GetType().Name);
                span.SetTag(ERROR_EXCEPTION_MESSAGE_TAG, exception.Message);
                span.SetTag(ERROR_STACK_TAG, exception.StackTrace);
                span.Finish(exception, SpanStatus.InternalError);
            }
            else
            {
                span.Finish(SpanStatus.UnknownError);
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
                    ReportHub.LogWarning(new ReportData(ReportCategory.ANALYTICS), $"({nameof(FinishAllTransactions)}) Error finishing transaction '{transactionKey.Name}' during application quit: {ex.Message}");
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
