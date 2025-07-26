using Dapper;
using Infrastructure.DTO;
using Infrastructure.YoLoUtil;
using MySql.Data.MySqlClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using YoloDotNet;
using YoloDotNet.Extensions;

namespace Infrastructure.Servers;

public class YoloServer
{
    private readonly MySqlConnection _connection;

    public YoloServer(MySqlConnection connection)
    {
        _connection = connection;
    }

    private static readonly string? BasePath =
        Directory.GetCurrentDirectory();

    public async Task Detection(AiTaskMessage message)
    {
        try
        {
            string base64 = message.Photo.Substring(message.Photo.IndexOf(',') + 1);
            byte[] data = Convert.FromBase64String(base64);
            using (MemoryStream ms = new MemoryStream(data))
            {
                var sbjgCount = 0;
                string base64Image = "";
                var result = "";
                using (var image = Image.Load<Rgba32>(ms))
                {
                    using var yolo = new Yolo(Path.Combine(BasePath, message.Path), false);
                    var results = yolo.RunObjectDetection(image, 0.3);
                    image.Draw(results);
                    sbjgCount = results.Count;

                    using (MemoryStream outputMs = new MemoryStream())
                    {
                        image.Save(outputMs, new JpegEncoder());
                        byte[] imageBytes = outputMs.ToArray();
                        base64Image = Convert.ToBase64String(imageBytes);
                    }
                }

                // 使用Dapper更新数据库
                var updateSql = @"
                UPDATE YoLoTbs 
                SET SbJgCount = @SbJgCount,
                    SbJg = @SbJg,
                    IsManualReview = 0
                WHERE Id = @Id";

                await _connection.ExecuteAsync(updateSql, new
                {
                    SbJgCount = sbjgCount,
                    SbJg = "目标监测识别完成，结果请看详情图片",
                    Id = message.TaskId
                });

                // 更新图片表
                var getPhotoIdSql = "SELECT PhotosId FROM YoLoTbs WHERE Id = @Id";
                var photoId = await _connection.QueryFirstOrDefaultAsync<long>(getPhotoIdSql, new { Id = message.TaskId });
                if (photoId > 0)
                {
                    var updatePhotoSql = @"
                UPDATE Photos 
                SET PhotoBase64 = @PhotoBase64
                WHERE PhotosId = @PhotoId";

                    await _connection.ExecuteAsync(updatePhotoSql, new
                    {
                        PhotoBase64 = "data:image/jpeg;base64," + base64Image,
                        PhotoId = photoId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // 异常处理：更新数据库错误状态
            var errorSql = @"
            UPDATE YoLoTbs 
            SET SbJg = @ErrorMsg,
                IsManualReview = 1
            WHERE Id = @Id";
            await _connection.ExecuteAsync(errorSql, new
            {
                ErrorMsg = $"处理失败: {ex.Message}",
                Id = message.TaskId
            });
        }
    }

    public async Task Classification(AiTaskMessage message)
    {
        try
        {
            string base64 = message.Photo.Substring(message.Photo.IndexOf(',') + 1);
            byte[] data = Convert.FromBase64String(base64);
            using (MemoryStream ms = new MemoryStream(data))
            {
                var sbjgCount = 0;
                var result = "";
                using (var image = Image.Load<Rgba32>(ms))
                {
                    using var yolo = new Yolo(Path.Combine(BasePath, message.Path), false);
                    var runClassification = yolo.RunClassification(image);
                    if (runClassification[0].Confidence < 0.8)
                    {
                        result = "未识别出来！";
                    }
                    else
                    {
                        if (message.ModelName == "动物识别")
                        {
                            result = YoloClassAnimalUtil.GetAnimalName(runClassification[0].Label);
                        }
                        else
                        {
                            result = runClassification[0].Label;
                        }
                    }

                    var updateSql = @"
                UPDATE YoLoTbs 
                SET 
                    SbJg = @SbJg,
                    IsManualReview = 0
                WHERE Id = @Id";

                    await _connection.ExecuteAsync(updateSql, new
                    {
                        SbJg = result,
                        Id = message.TaskId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // 异常处理：更新数据库错误状态
            var errorSql = @"
            UPDATE YoLoTbs 
            SET SbJg = @ErrorMsg,
                IsManualReview = 1
            WHERE Id = @Id";
            await _connection.ExecuteAsync(errorSql, new
            {
                ErrorMsg = $"处理失败: {ex.Message}",
                Id = message.TaskId
            });
        }
    }
}