namespace TestAppCommon;

public class RetryInfo
{
    public int RetryCount { get; set; }

    public int AttemptedCount { get; set; }

    public bool AlwaysThrow { get; set; }
}

public class RetryException : Exception
{
    public RetryException(int attempt)
        :this(attempt, string.Empty)
    {

    }

    public RetryException(int attempt, string message)
        :base(message: $"Attempt #{attempt}. Error: {message}.")
    {

    }
}