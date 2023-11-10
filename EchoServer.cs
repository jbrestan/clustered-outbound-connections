using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ConnectionsSample;

/// <summary>
/// Just for convenience of testing the connections in-process without a standalone mock server.
/// Don't use as an example for good server implementation.
/// </summary>
public class EchoServer : IHostedService
{
    private readonly TcpListener _server = new(IPAddress.Any, 8081);
    private readonly CancellationTokenSource _serverShutdownSource = new();
    private readonly ConcurrentBag<TcpClient> _clients = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        Task.Run(Serve, _serverShutdownSource.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serverShutdownSource.Cancel();
        _server?.Stop();
        DisconnectClients();
        return Task.CompletedTask;
    }

    private async Task Serve()
    {
        while (!_serverShutdownSource.Token.IsCancellationRequested)
        {
            // Accept connection, save the client instance for graceful shutdown
            // and start a thread that receives messages per client.
            var client = await _server.AcceptTcpClientAsync(_serverShutdownSource.Token);
            _clients.Add(client);
            _ = Task.Run(async () => await ClientLoop(client), _serverShutdownSource.Token);
        }
    }

    private async Task ClientLoop(TcpClient client)
    {
        await using NetworkStream stream = client.GetStream();
        var buffer = ArrayPool<byte>.Shared.Rent(client.ReceiveBufferSize);;
        try
        {
            while (client.Connected)
            {
                // Ignore messages larger than buffer size, but don't do this in production.
                var readBytes = await stream.ReadAsync(buffer, _serverShutdownSource.Token);
                // It's an echo server, so just send back the original payload.
                await stream.WriteAsync(buffer.AsMemory(..readBytes) , _serverShutdownSource.Token);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void DisconnectClients()
    {
        foreach (var client in _clients)
        {
            client.Close();
        }
    }
}
