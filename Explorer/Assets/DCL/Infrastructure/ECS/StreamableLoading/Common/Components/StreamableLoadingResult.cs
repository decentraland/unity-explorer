﻿using AssetManagement;
using DCL.Diagnostics;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     The final result of the request
    /// </summary>
    public readonly struct StreamableLoadingResult<T>
    {
        /// <summary>
        ///     Always contains result even if the request has failed
        /// </summary>
        public readonly struct WithFallback
        {
            public readonly T Asset;

            /// <summary>
            ///     Initialized won't be set if default constructor was called
            /// </summary>
            private readonly bool initialized;

            public WithFallback(T asset)
            {
                Asset = asset;
                initialized = true;
            }

            /// <summary>
            ///     Can be uninitialized if structure was created with default constructor
            /// </summary>
            public bool IsInitialized => initialized;

            public static implicit operator StreamableLoadingResult<T>(WithFallback withFallback) =>
                withFallback.IsInitialized ? new StreamableLoadingResult<T>(withFallback.Asset) : new StreamableLoadingResult<T>();
        }

        private readonly (ReportData reportData, Exception exception)? exceptionData;

        public readonly bool Succeeded;
        public readonly T? Asset;
        public Exception? Exception => exceptionData?.exception;
        public ReportData ReportData => exceptionData?.reportData ?? ReportData.UNSPECIFIED;

        public StreamableLoadingResult(T? asset) : this()
        {
            Asset = asset;
            Succeeded = true;
        }

        public StreamableLoadingResult(ReportData reportData, Exception exception) : this()
        {
            if (exception is not OperationCanceledException)
            {
                if (exception is StreamableLoadingException streamableLoadingException)
                {
                    switch (streamableLoadingException.Severity)
                    {
                        case LogType.Exception:
                            ReportHub.LogException(exception, reportData);
                            break;
                        default:
                            ReportHub.Log(streamableLoadingException.Severity, reportData, exception.ToString());
                            break;
                    }
                }
                else
                    ReportHub.LogException(exception, reportData);
            }


            exceptionData = (reportData, exception);
        }

        public bool IsInitialized => Exception != null || Asset != null || Succeeded;

        public override string ToString() =>
            IsInitialized ? Succeeded ? Asset!.ToString() : Exception!.ToString() : "Not Initialized";
    }
}
