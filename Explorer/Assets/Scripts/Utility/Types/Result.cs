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
}
