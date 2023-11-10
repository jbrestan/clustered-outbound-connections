using ConnectionsSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseOrleans(orleans =>
{
    orleans.UseLocalhostClustering();
    orleans.UseInMemoryReminderService();
});

builder.Services.AddHostedService<EchoServer>();

builder.Services.AddHostedService<OrleansInitializer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapPost("/send-sample-message", async (IGrainFactory grainFactory) =>
{
    await grainFactory.GetGrain<IConnectionHandlerGrain>(Guid.Empty).Send(new byte[128]);
});

app.Run();
