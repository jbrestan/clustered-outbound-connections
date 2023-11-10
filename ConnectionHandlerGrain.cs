using System.Buffers;
using System.Net.Sockets;
using Orleans.Runtime;
using Orleans.Timers;

namespace ConnectionsSample;

public interface IConnectionHandlerGrain : IGrainWithGuidKey
{
    Task RegisterSelfActivation();
}

public class ConnectionHandlerGrain : IGrainBase, IConnectionHandlerGrain, IRemindable
{
    private const string ActivationReminderName = "ConnectionHandlerGrainActivationReminder";

    private readonly ILogger<ConnectionHandlerGrain> _logger;
    private TcpClient? _tcpClient;

    public IGrainContext GrainContext { get; }

#region Grain lifecycle

    public ConnectionHandlerGrain(
        IGrainContext grainContext,
        ITimerRegistry timerRegistry,
        ILogger<ConnectionHandlerGrain> logger)
    {
        GrainContext = grainContext;
        _logger = logger;

        // Register timer that ensures connection is open and sends a ping.
        timerRegistry.RegisterTimer(
            GrainContext,
            state => ((ConnectionHandlerGrain)state).EnsureConnectionHealthAsync(),
            this,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5));
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ActivationReminderName)
        {
            _logger.LogInformation("Received activation reminder");
        }

        return Task.CompletedTask;
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

    private async Task EnsureConnectionHealthAsync()
    {
        if (_tcpClient is null)
        {
            _logger.LogInformation("Establishing connection");
            _tcpClient = await CreateClientAsync();
        }

        if (_tcpClient.Connected)
        {
            var stream = _tcpClient.GetStream();
            // Simulate ping
            await stream.WriteAsync(new byte[] { 1, 2, 3 });
        }

        // The connection state can change even after the previous send operation.
        if (!_tcpClient.Connected)
        {
            _tcpClient.Close();
            _tcpClient = null;
        }
    }

    private async Task<TcpClient> CreateClientAsync()
    {
        var client = new TcpClient();
        client.SendTimeout = 5000;
        // TODO: The connection info should either come with RegisterSelfActivation or be queried later.
        await client.ConnectAsync("localhost", 8081);

        _ = Task.Run(StartReceivingDataAsync);

        return client;

        async Task StartReceivingDataAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(client.ReceiveBufferSize);
            try
            {
                await using var stream = client.GetStream();
                while (client.Connected)
                {
                    // Ignore messages larger than buffer size, but don't do this in production.
                    var bytesReceived = await stream.ReadAsync(buffer);
                    await OnDataReceived(buffer.AsMemory(..bytesReceived));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private async Task OnDataReceived(Memory<byte> data)
    {
        _logger.LogInformation($"Received {data.Length} bytes");
    }
}
