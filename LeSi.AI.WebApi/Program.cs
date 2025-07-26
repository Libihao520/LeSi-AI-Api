using Infrastructure;
using Infrastructure.Servers;
using MySql.Data.MySqlClient;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("MySQL");
builder.Services.AddSingleton(_ => new MySqlConnection(connectionString));

// 添加 RabbitMQ 连接服务
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "admin",
        Password = "password"
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