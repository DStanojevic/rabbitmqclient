using System;

namespace RabbitMqClient;

public interface IProcessingOutcome
{
}

public class FailureProcessingOutcome : IProcessingOutcome
{
    public FailureProcessingOutcome(Exception exception)
    {
        Error = exception ?? throw new ArgumentNullException(nameof(exception));
    }
    public Exception Error { get; }
}

public class NegativelyAcknowledgedOutcome : IProcessingOutcome
{
    public NegativelyAcknowledgedOutcome(string reason)
    {
        Reason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentNullException(nameof(reason)) : reason;
    }
    public string Reason { get; }
}

public class RejectedOutcome : IProcessingOutcome
{
    public RejectedOutcome(string reason)
    {
        Reason = reason;
    }
    public string Reason { get; }
}

public class AcknowledgedOutcome : IProcessingOutcome
{
}