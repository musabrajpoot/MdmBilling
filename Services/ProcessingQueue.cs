using System.Threading.Channels;
namespace MdmBilling.Services;
// In-memory queue of run IDs. The database is the real record of every run.
public class ProcessingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
    public ValueTask EnqueueAsync(int runId, CancellationToken ct = default)
    =>
    _channel.Writer.WriteAsync(runId, ct);
    public ValueTask<int> DequeueAsync(CancellationToken ct) =>
    _channel.Reader.ReadAsync(ct);
}
