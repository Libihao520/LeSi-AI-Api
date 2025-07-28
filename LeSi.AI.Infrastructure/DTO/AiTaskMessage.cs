namespace Infrastructure.DTO;

public class AiTaskMessage
{
    public long TaskId { get; set; }

    public string FrontendTaskId { get; set; }
    
    public long ModelId { get; set; }

    public string ModelCls { get; set; }
    public string ModelName { get; set; }
    public string Photo { get; set; }
    public string Path { get; set; }

    public string CallbackQueue { get; set; }
}