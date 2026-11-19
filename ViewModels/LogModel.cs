namespace MdmBilling.ViewModels;

public class AuditRow
{
    public DateTime Time { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Ip { get; set; }
}

public class ErrorRow
{
    public DateTime Time { get; set; }
    public int? RunId { get; set; }
    public string? Server { get; set; }
    public string Message { get; set; } = "";
}