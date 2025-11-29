namespace yQuant.Infra.Broker.KIS;

// Custom Exception for Rate Limiting
public class RateLimitExceededException : Exception
{
    public RateLimitExceededException() { }
    public RateLimitExceededException(string message) : base(message) { }
    public RateLimitExceededException(string message, Exception innerException) : base(message, innerException) { }
}
