// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KtxUnity.Editor
{
    // from glTFast : AsyncHelpers
    static class AsyncHelpers
    {
        /// <summary>
        /// Executes an async Task&lt;T&gt; method which has a T return type synchronously
        /// </summary>
        /// <typeparam name="T">Return Type</typeparam>
        /// <param name="task">Task&lt;T&gt; method to execute</param>
        /// <returns></returns>
        public static T RunSync<T>(Func<Task<T>> task)
        {
            var oldContext = SynchronizationContext.Current;
            var sync = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sync);
            var ret = default(T);
            // ReSharper disable once AsyncVoidLambda
            sync.Post(async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch (Exception e)
                {
                    sync.InnerException = e;
                    throw;
                }
                finally
                {
                    sync.EndMessageLoop();
                }
            }, null);
            sync.BeginMessageLoop();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            return ret;
        }

        class ExclusiveSynchronizationContext : SynchronizationContext
        {
            bool m_Done;
            public Exception InnerException { get; set; }
            readonly AutoResetEvent m_WorkItemsWaiting = new AutoResetEvent(false);
            readonly Queue<Tuple<SendOrPostCallback, object>> m_Items =
                new Queue<Tuple<SendOrPostCallback, object>>();

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException("We cannot send to our same thread");
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (m_Items)
                {
                    m_Items.Enqueue(Tuple.Create(d, state));
                }
                m_WorkItemsWaiting.Set();
            }

            public void EndMessageLoop()
            {
                Post(_ => m_Done = true, null);
            }

            public void BeginMessageLoop()
            {
                while (!m_Done)
                {
                    Tuple<SendOrPostCallback, object> task = null;
                    lock (m_Items)
                    {
                        if (m_Items.Count > 0)
                        {
                            task = m_Items.Dequeue();
                        }
                    }
                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null) // the method threw an exception
                        {
                            throw new AggregateException("AsyncHelpers.Run method threw an exception.", InnerException);
                        }
                    }
                    else
                    {
                        m_WorkItemsWaiting.WaitOne();
                    }
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }
        }
    }
}
