using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Controllers;

[Authorize(Roles = AppRoles.ViewLogs)]
public class LogsController : BaseController
{
    private const int PageSize = 50;
    private readonly AppDbContext _db;

    public LogsController(AppDbContext db) => _db = db;

    // /Logs  goes to the audit log
    public IActionResult Index() => RedirectToAction(nameof(Audit));

    public async Task<IActionResult> Audit(int page = 1)
    {
        page = Math.Max(1, page);
        var total = await _db.AuditLogs.CountAsync();

        var rows = await (from a in _db.AuditLogs.AsNoTracking()
                          join u in _db.Users on a.UserId equals u.Id into uj
                          from u in uj.DefaultIfEmpty()
                          orderby a.Id descending
                          select new AuditRow
                          {
                              Time = a.Timestamp,
                              User = u != null ? u.FullName : "",
                              Action = a.Action,
                              Entity = a.EntityName,
                              EntityId = a.EntityId,
                              Ip = a.IpAddress
                          })
                         .Skip((page - 1) * PageSize).Take(PageSize)
                         .ToListAsync();

        return View(new PagedResult<AuditRow>
        {
            Items = rows, Page = page, PageSize = PageSize, TotalCount = total
        });
    }

    public async Task<IActionResult> Errors(int page = 1)
    {
        page = Math.Max(1, page);
        var total = await _db.ErrorLogs.CountAsync();

        var rows = await (from e in _db.ErrorLogs.AsNoTracking()
                          join s in _db.Servers on e.ServerId equals (int?)s.Id into sj
                          from s in sj.DefaultIfEmpty()
                          orderby e.Id descending
                          select new ErrorRow
                          {
                              Time = e.Timestamp,
                              RunId = e.RunId,
                              Server = s != null ? s.Name : null,
                              Message = e.Message
                          })
                         .Skip((page - 1) * PageSize).Take(PageSize)
                         .ToListAsync();

        return View(new PagedResult<ErrorRow>
        {
            Items = rows, Page = page, PageSize = PageSize, TotalCount = total
        });
    }
}

