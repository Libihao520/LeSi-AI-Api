using Infrastructure;
using Infrastructure.Servers;
using MySql.Data.MySqlClient;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile(
        $"appsettings.{(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ? "Docker" : "Development")}.json",
        optional: true);

builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("MySQL");
builder.Services.AddSingleton(_ => new MySqlConnection(connectionString));

// 添加 RabbitMQ 连接服务
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory()
    {
        HostName = rabbitConfig["HostName"],
        Port = int.Parse(rabbitConfig["Port"]!),
        UserName = rabbitConfig["UserName"],
        Password = rabbitConfig["Password"],
        DispatchConsumersAsync = true,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
    };
    return factory.CreateConnection();
});
builder.Services.AddHostedService<AiProcessingWorker>();
builder.Services.AddSingleton<YoloServer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.Run();