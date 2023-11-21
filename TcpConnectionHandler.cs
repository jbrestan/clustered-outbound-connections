using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace ConnectionsSample;

public sealed class TcpConnectionHandler : IDisposable
{
    private TcpClient _client = new ();
    private readonly CancellationTokenSource _cancellation = new ();
    private readonly Func<ReadOnlySequence<byte>, CancellationToken, Task> _onMessageReceived;

    public TcpConnectionHandler(Func<ReadOnlySequence<byte>, CancellationToken, Task> onMessageReceived)
    {
        _onMessageReceived = onMessageReceived;
    }

    public async Task SendData(byte[] payload)
    {
        _cancellation.Token.ThrowIfCancellationRequested();
        await EnsureConnection();
        await _client.GetStream().WriteAsync(payload, _cancellation.Token);
    }

    public async Task SendPing()
    {
        await SendData(new byte[] {1,2,3});
    }

    public async Task EnsureConnection()
    {
        if(!_client.Connected && !_cancellation.IsCancellationRequested)
        {
            _client = new ();
            await _client.ConnectAsync("localhost", 8081);
            _ = Task.Run(async () => await ReceiveDataAsync(_client, _cancellation.Token));
        }
    }

    private async Task ReceiveDataAsync(TcpClient client, CancellationToken cancellation)
    {
        var reader = PipeReader.Create(client.GetStream(), new StreamPipeReaderOptions(leaveOpen: true));
        try
        {
            while (client.Connected && !cancellation.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellation);
                var buffer = readResult.Buffer;
                if (!buffer.IsEmpty)
                {
                    // Buffer should be parsed into messages
                    await _onMessageReceived(buffer, _cancellation.Token);
                    reader.AdvanceTo(buffer.End);
                }
            }
        }
        finally
        {
            client.Dispose();
            await reader.CompleteAsync();
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _client.Dispose();
    }
}
