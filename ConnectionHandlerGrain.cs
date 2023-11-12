using System.Buffers;
using System.Net.Sockets;
using Orleans.Runtime;

namespace ConnectionsSample;

public interface IConnectionHandlerGrain : IGrainWithGuidKey
{
    Task RegisterSelfActivation();
    Task Send(byte[] payload);
}

public class ConnectionHandlerGrain : IGrainBase, IConnectionHandlerGrain, IRemindable
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

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ActivationReminderName)
        {
            _logger.LogInformation("Received activation reminder and sending ping to server.");
            await _tcpClient.SendPing();
        }
    }

    public async Task RegisterSelfActivation()
    {
        // Register a reminder that activates this grain in case it gets deactivated.
        await this.RegisterOrUpdateReminder(
            ActivationReminderName,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1));
    }

#endregion

    public async Task Send(byte[] payload)
    {
        await _tcpClient.SendData(payload);
    }

    private async Task OnDataReceived(Memory<byte> data)
    {
        _logger.LogInformation($"Received {data.Length} bytes from server");
    }
}
