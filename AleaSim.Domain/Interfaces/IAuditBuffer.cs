using AleaSim.Domain.Entities;
using System.Threading.Channels;

namespace AleaSim.Domain.Interfaces;

public interface IAuditBuffer {
    void Enqueue(AuditEvent auditEvent);
    IAsyncEnumerable<AuditEvent> DequeueAllAsync(CancellationToken ct);
}

public class AuditBuffer : IAuditBuffer {
    private readonly Channel<AuditEvent> _channel;

    public AuditBuffer() {
        // Unbounded channel for high throughput logs
        _channel = Channel.CreateUnbounded<AuditEvent>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enqueue(AuditEvent auditEvent) {
        _channel.Writer.TryWrite(auditEvent);
    }

    public IAsyncEnumerable<AuditEvent> DequeueAllAsync(CancellationToken ct) {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
