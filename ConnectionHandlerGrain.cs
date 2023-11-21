using System.Buffers;
using System.Net.Sockets;
using Orleans.Runtime;

namespace ConnectionsSample;

public interface IConnectionHandlerGrain : IGrainWithGuidKey
{
    Task RegisterSelfActivation();
    Task Send(byte[] payload);
}

public class ConnectionHandlerGrain : Grain, IConnectionHandlerGrain, IRemindable
{
    private const string ActivationReminderName = "ConnectionHandlerGrainActivationReminder";

    private readonly ILogger<ConnectionHandlerGrain> _logger;
    private readonly TcpConnectionHandler _tcpClient;

    public IGrainContext GrainContext { get; }

#region Grain lifecycle

    public ConnectionHandlerGrain(
        IGrainContext grainContext,
        ILogger<ConnectionHandlerGrain> logger)
    {
        GrainContext = grainContext;
        _logger = logger;
        _tcpClient = new(OnDataReceived);
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"[{GrainContext.GrainId}] Activating");
        await base.OnActivateAsync(cancellationToken);
        await _tcpClient.EnsureConnection();
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"[{GrainContext.GrainId}] Deactivating");
        _tcpClient.Dispose();
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ActivationReminderName)
        {
            _logger.LogInformation($"[{GrainContext.GrainId}] Received activation reminder and sending ping to server");
            await _tcpClient.SendPing();
        }
    }

    public async Task RegisterSelfActivation()
    {
        _logger.LogInformation($"[{GrainContext.GrainId}] Registering activation reminder");
        // Register a reminder that activates this grain in case it gets deactivated.
        await this.RegisterOrUpdateReminder(
            ActivationReminderName,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1));
    }

#endregion

    public async Task Send(byte[] payload)
    {
        _logger.LogInformation($"[{GrainContext.GrainId}] Sending {payload.Length} bytes of data to server");
        await _tcpClient.SendData(payload);
    }

    private Task OnDataReceived(ReadOnlySequence<byte> data, CancellationToken cancellation)
    {
        _logger.LogInformation($"[{GrainContext.GrainId}] Received {data.Length} bytes from server");
        return Task.CompletedTask;
    }
}
