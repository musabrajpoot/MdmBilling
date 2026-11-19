namespace MdmBilling.Services;
public class ProcessingOptions
{
public int MaxParallelServers { get; set; } = 3;
public int ChunkSize { get; set; } = 500;
public int MaxRetries { get; set; } = 2;
public int RetryDelaySeconds { get; set; } = 5;
}
