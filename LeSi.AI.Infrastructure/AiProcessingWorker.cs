using System.Text;
using System.Text.Json;
using Infrastructure.DTO;
using Infrastructure.Servers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure;

public class AiProcessingWorker : BackgroundService
{
    private readonly IConnection _rabbitMqConnection;
    private readonly ILogger<AiProcessingWorker> _logger;
    private readonly YoloServer _aiService;

    public AiProcessingWorker(IConnection rabbitMqConnection, ILogger<AiProcessingWorker> logger, YoloServer aiService)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _logger = logger;
        _aiService = aiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var channel = _rabbitMqConnection.CreateModel())
        {
            channel.QueueDeclare(queue: "ai_yolo_recognition_tasks",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<AiTaskMessage>(Encoding.UTF8.GetString(body));

                try
                {
                    _logger.LogInformation($"Processing task {message.TaskId}");
                    string sbjg = "";
                    if (message.ModelCls == "目标监测")
                    {
                        sbjg = await _aiService.Detection(message);
                    }
                    else
                    {
                        sbjg = await _aiService.Classification(message);
                    }

                    _logger.LogInformation($"Task {message.TaskId} completed");


                    if (!string.IsNullOrEmpty(message.CallbackQueue))
                    {
                        var result = new
                        {
                            TaskId = message.TaskId,
                            FrontendTaskId = message.FrontendTaskId,
                            Status = "Completed",
                            Timestamp = DateTime.UtcNow,
                            SbJg = sbjg
                        };

                        var resultBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
                        channel.BasicPublish(
                            exchange: "",
                            routingKey: message.CallbackQueue,
                            basicProperties: null,
                            body: resultBody);
                    }

                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing task {message.TaskId}");
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            channel.BasicConsume(queue: "ai_yolo_recognition_tasks",
                autoAck: false,
                consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}