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
    }

    public readonly struct EnumResult<TErrorEnum>
    {
        public readonly (TErrorEnum State, string Message)? Error;

        public bool Success => Error == null;

        private EnumResult((TErrorEnum State, string Message)? error)
        {
            this.Error = error;
        }

        public static EnumResult<TErrorEnum> SuccessResult() =>
            new (null);

        public static EnumResult<TErrorEnum> ErrorResult(TErrorEnum state, string errorMessage) =>
            new ((state, errorMessage));
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

        public static EnumResult<TValue, TErrorEnum> ErrorResult(TErrorEnum state, string errorMessage) =>
            new (default(TValue)!, (state, errorMessage));
    }
}
