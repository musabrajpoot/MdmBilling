using MdmBilling.Models;
using MdmBilling.Services;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MdmBilling.Controllers;

[Authorize(Roles = AppRoles.ViewResults)]
public class BatchesController : BaseController
{
    private readonly BatchService _batches;

    public BatchesController(BatchService batches) => _batches = batches;

    public async Task<IActionResult> Index() => View(await _batches.GetBatchesAsync());

    public async Task<IActionResult> Details(int id)
    {
        var batch = await _batches.GetBatchAsync(id);
        return batch is null ? NotFound() : View(batch);
    }

    // ---------------- Upload ----------------

    [HttpGet, Authorize(Roles = AppRoles.RunBatches)]
    public async Task<IActionResult> Upload()
    {
        return View(new UploadBatchViewModel { Servers = await _batches.GetServerOptionsAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = AppRoles.RunBatches)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(UploadBatchViewModel model)
    {
        if (model.File is null || model.File.Length == 0)
            ModelState.AddModelError(nameof(model.File), "Select an Excel file.");
        else if (!Path.GetExtension(model.File.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.File), "Only .xlsx files are allowed.");

        if (model.SelectedServerIds.Count == 0)
            ModelState.AddModelError(nameof(model.SelectedServerIds), "Select at least one server.");

        if (!ModelState.IsValid)
        {
            model.Servers = await _batches.GetServerOptionsAsync();
            return View(model);
        }

        await using var stream = model.File!.OpenReadStream();
        var result = await _batches.UploadAsync(model.BatchNumber, model.BillingDate,
            model.SelectedServerIds, model.File.FileName, stream, UserId);

        if (result.Errors.Count > 0)
        {
            model.Errors = result.Errors;
            model.Warnings = result.Warnings;
            model.Servers = await _batches.GetServerOptionsAsync();
            return View(model);
        }

        TempData["Ok"] = $"Batch {model.BatchNumber} saved with {result.MeterCount} meters.";
        return RedirectToAction(nameof(Details), new { id = result.BatchId });
    }

    // ---------------- Start, Run page, progress ----------------

    // Opening /Batches/Start/1 in the address bar sends the user back to the batch page.
    [HttpGet]
    public IActionResult Start(int id) => RedirectToAction(nameof(Details), new { id });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = AppRoles.RunBatches)]
    public async Task<IActionResult> Start(int id, List<int> selectedServerIds)
    {
        try
        {
            var runId = await _batches.StartRunAsync(id, selectedServerIds, UserId);
            return RedirectToAction(nameof(Run), new { id = runId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // The page you were missing: /Batches/Run/4
    public async Task<IActionResult> Run(int id, string? filter, int page = 1)
    {
        var progress = await _batches.GetRunProgressAsync(id);
        if (progress is null) return NotFound();

        ViewBag.Filter = filter ?? "all";
        var results = progress.IsFinished ? await _batches.GetRunResultsAsync(id, filter, page) : null;
        return View(new RunViewModel { Progress = progress, Results = results });
    }

    // Called every few seconds by the Run page while the run is working.
    [HttpGet]
    public async Task<IActionResult> RunProgress(int id)
    {
        var p = await _batches.GetRunProgressAsync(id);
        if (p is null) return NotFound();

        return Json(new
        {
            status = p.Status.ToString(),
            isFinished = p.IsFinished,
            percent = p.Percent,
            serversDone = p.ServersDone,
            serversTotal = p.ServersTotal,
            serversFailed = p.ServersFailed
        });
    }
}