using System.Security.Claims;
using MdmBilling.Data;
using MdmBilling.Models;
namespace MdmBilling.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }
    public async Task LogAsync(string action, string? entityName = null,
    string? entityId = null, string? userId = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ??
        ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,

            EntityName = entityName,
            EntityId = entityId,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }
}