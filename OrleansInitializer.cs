namespace ConnectionsSample;

// Startup process:
// 1. Get list of integrations to connect to
// 2. For each integration make sure the cluster has a reminder to activate the grain
// 3. Connect to messaging and subscribe to topic
// 4. OnMessage get grain by ID and send message to it

public class OrleansInitializer : IHostedService
{
    private readonly IGrainFactory _grainFactory;

    public OrleansInitializer(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // TODO: Get more realistic list of integrations
        var integrations = new[] { Guid.NewGuid() };
        foreach (var integrationId in integrations)
        {
            var handler = _grainFactory.GetGrain<IConnectionHandlerGrain>(integrationId);
            await handler.RegisterSelfActivation();
        }

        // TODO: Connect to service bus to receive messages
        // ALTERNATIVE: Don't use service bus, expose an API endpoint like
        //      POST /send-xml { 'xmlMessage': '<!-- foo -->' } and route from there.
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
