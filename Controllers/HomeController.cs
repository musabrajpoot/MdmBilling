using System.Diagnostics;
using MdmBilling.Models;
using MdmBilling.Services;
using Microsoft.AspNetCore.Mvc;

namespace MdmBilling.Controllers;

public class HomeController : BaseController
{
    private readonly DashboardService _dashboard;

    public HomeController(DashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index() => View(await _dashboard.GetDashboardAsync());

    // Called by the dashboard with JavaScript: the health of every server as JSON.
    [HttpGet]
    public async Task<IActionResult> ServerHealth(bool refresh = false) =>
        Json(await _dashboard.GetServerHealthAsync(refresh, HttpContext.RequestAborted));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}