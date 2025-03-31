using System;
using UnityEngine.Assertions;
using System.Threading;

namespace Utility.Types
{
    public readonly struct Result
    {
        public readonly bool Success;
        public readonly string? ErrorMessage;

        private Result(bool success, string? errorMessage)
        {
            this.Success = success;
            this.ErrorMessage = errorMessage;
        }

        public static Result SuccessResult() =>
            new (true, null);

        public static Result ErrorResult(string errorMessage) =>
            new (false, errorMessage);

        public static Result CancelledResult() =>
            new (false, nameof(OperationCanceledException));

        public EnumResult<TErrorEnum> AsEnumResult<TErrorEnum>(TErrorEnum inErrorCase) =>
            Success
                ? EnumResult<TErrorEnum>.SuccessResult()
                : EnumResult<TErrorEnum>.ErrorResult(inErrorCase, ErrorMessage!);
    }

    public readonly struct Result<T>
    {
        public readonly T Value;
        public readonly string? ErrorMessage;

        public bool Success => ErrorMessage == null;

        private Result(T value, string? errorMessage)
        {
            this.Value = value;
            this.ErrorMessage = errorMessage;
        }

        public static Result<T> SuccessResult(T value) =>
            new (value, null);

        public static Result<T> ErrorResult(string errorMessage) =>
            new (default(T)!, errorMessage);

        public static Result<T> CancelledResult() =>
            new (default(T)!, nameof(OperationCanceledException));

        public static implicit operator Result<T>(Result result)
        {
            Assert.IsFalse(result.Success);
            return ErrorResult(result.ErrorMessage!);
        }

        public static implicit operator Result(Result<T> result) =>
            result.Success ? Result.SuccessResult() : Result.ErrorResult(result.ErrorMessage!);

        public EnumResult<TErrorEnum> AsEnumResult<TErrorEnum>(TErrorEnum inErrorCase) =>
            Success
                ? EnumResult<TErrorEnum>.SuccessResult()
                : EnumResult<TErrorEnum>.ErrorResult(inErrorCase, ErrorMessage!);
    }

    public readonly struct EnumResult<TErrorEnum>
    {
        public readonly (TErrorEnum State, string Message, Exception Exception)? Error;

        public bool Success => Error == null;

        private EnumResult((TErrorEnum State, string Message, Exception Exception)? error)
        {
            this.Error = error;
        }

        public static EnumResult<TErrorEnum> SuccessResult() =>
            new (null);

        public static EnumResult<TErrorEnum> ErrorResult(TErrorEnum state, string errorMessage = "", Exception exception = null) =>
            new ((state, errorMessage, exception));

        public static EnumResult<TErrorEnum> CancelledResult(TErrorEnum state) =>
            ErrorResult(state, nameof(OperationCanceledException));

        public Result AsResult()
        {
            if (Success)
                return Result.SuccessResult();

            var error = Error!.Value;
            return Result.ErrorResult($"{error.State}: {error.Message}");
        }

        public EnumResult<TOther> As<TOther>(TOther inErrorCase) =>
            Success
                ? EnumResult<TOther>.SuccessResult()
                : EnumResult<TOther>.ErrorResult(inErrorCase, Error!.Value.Message!);

        public EnumResult<TOther> As<TOther>(Func<TErrorEnum, TOther> mapping) =>
            Success
                ? EnumResult<TOther>.SuccessResult()
                : EnumResult<TOther>.ErrorResult(mapping(Error!.Value.State), Error!.Value.Message!);
    }

    public readonly struct EnumResult<TValue, TErrorEnum>
    {
        public readonly TValue Value;
        public readonly (TErrorEnum State, string Message)? Error;

        public bool Success => Error == null;

        private EnumResult(TValue value, (TErrorEnum State, string Message)? error)
        {
            this.Value = value;
            this.Error = error;
        }

        public static EnumResult<TValue, TErrorEnum> SuccessResult(TValue value) =>
            new (value, null);

        public static EnumResult<TValue, TErrorEnum> ErrorResult(TErrorEnum state, string errorMessage = "") =>
            new (default(TValue)!, (state, errorMessage));

        public static bool TryErrorIfCancelled(CancellationToken token, out EnumResult<TValue, TErrorEnum> result)
        {
            if (token.IsCancellationRequested)
            {
                result = ErrorResult(default(TErrorEnum)!, "Operation was cancelled");
                return true;
            }

            result = SuccessResult(default(TValue)!);
            return false;
        }

        public TValue Unwrap() =>
            Success
                ? Value
                : throw new InvalidOperationException(
                    $"Cannot unwrap error result: {Error!.Value.State} - {Error!.Value.Message}"
                );

        public override string ToString() =>
            $"EnumResult<{typeof(TValue).Name}, {typeof(TErrorEnum).Name}>: {(Success ? "Success" : $"Error: {Error!.Value.State} - {Error.Value.Message}")}";
    }

    /// <summary>
    /// Used for cases when none is expected to be returned, difference from Nullable that it can handle both struct and class
    /// </summary>
    public readonly struct Option<T>
    {
        public readonly T Value;
        public readonly bool Has;

        public static Option<T> None => new ();

        public static Option<T> Some(T value) =>
            new (value, true);

        private Option(T value, bool has)
        {
            this.Value = value;
            this.Has = has;
        }
    }

    public enum TaskError
    {
        MessageError,
        Timeout,
        Cancelled,
        UnexpectedException,
    }

    public static class ResultExtensions
    {
        public static string AsMessage<TErrorEnum>(this (TErrorEnum State, string Message, Exception exception)? error)
        {
            if (error.HasValue == false)
                return "Not an error";

            (TErrorEnum state, string message, Exception exception) = error!.Value;
            return $"{state}: {message}";
        }

        public static void EnsureSuccess(this Result result, string errorMessage)
        {
            if (result.Success == false)
                throw new Exception($"Result is failure: {errorMessage}");
        }
    }
}
