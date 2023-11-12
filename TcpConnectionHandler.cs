using System.Buffers;
using System.Net.Sockets;

namespace ConnectionsSample;

public class TcpConnectionHandler : IDisposable
{
    private TcpClient? _client = null;
    private NetworkStream? _stream = null;
    private bool _isDisposed = false;
    private readonly Func<Memory<byte>, Task> _onMessageReceived;

    public TcpConnectionHandler(Func<Memory<byte>, Task> onMessageReceived)
    {
        _onMessageReceived = onMessageReceived;
    }

    public async Task SendData(byte[] payload)
    {
        await EnsureConnection();
        if (_stream is not null)
        {
            await _stream.WriteAsync(payload);
        }
        else
        {
            throw new IOException("Client not connected.");
        }
    }

    public async Task SendPing()
    {
        await SendData(new byte[] {1,2,3});
    }

    private async Task EnsureConnection()
    {
        if(!_isDisposed)
        {
            if (_client is not null && !_client.Connected)
            {
                _client?.Dispose();
                _client = null;
                _stream = null;
            }

            if(_client is null)
            {
                _client = await CreateClientAsync();
                _stream = _client.GetStream();
            }
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
                // TODO: This needs better stream lifecycle handling.
                await using var stream = client.GetStream();
                while (client.Connected)
                {
                    // Ignore messages larger than buffer size, but don't do this in production.
                    var bytesReceived = await stream.ReadAsync(buffer);
                    if (bytesReceived > 0)
                    {
                        await _onMessageReceived(buffer.AsMemory(..bytesReceived));
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public void Dispose()
    {
        if(!_isDisposed)
        {
            _client?.Dispose();
            _isDisposed = true;
        }
    }
}
