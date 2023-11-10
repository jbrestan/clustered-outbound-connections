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
            state => ((ConnectionHandlerGrain)state).EnsureConnectionHealth(),
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

    public IGrainContext GrainContext { get; }

    public async Task RegisterSelfActivation()
    {
        // Register a reminder that activates this grain in case it gets deactivated.
        await this.RegisterOrUpdateReminder(
            ActivationReminderName,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1));
    }

    public async Task EnsureConnectionHealth()
    {
        _logger.LogInformation("Establishing connection");
        return;
    }
}
