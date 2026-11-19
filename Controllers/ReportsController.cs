
using MdmBilling.Models;
using MdmBilling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MdmBilling.Controllers;

[Authorize(Roles = AppRoles.DownloadReports)]
public class ReportsController : BaseController
{
    private readonly ReportService _reports;
    private readonly UserManager<ApplicationUser> _users;

    public ReportsController(
        ReportService reports,
        UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _users = users;
    }

    public async Task<IActionResult> Download(
        int runId,
        ReportFormat format)
    {
        // Super Admin can always download.
        // Other users need the CanDownloadReports switch.
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var user = await _users.GetUserAsync(User);

            if (user is null || !user.CanDownloadReports)
            {
                return Forbid();
            }
        }

        try
        {
            var file = await _reports.GetAsync(
                runId,
                format,
                UserId
            );

            return File(
                file.Content,
                file.ContentType,
                file.FileName
            );
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;

            return RedirectToAction(
                "Run",
                "Batches",
                new { id = runId }
            );
        }
    }
}
