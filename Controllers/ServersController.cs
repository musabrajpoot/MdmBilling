
using MdmBilling.Models;
using MdmBilling.Services;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MdmBilling.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class ServersController : BaseController
{
    private readonly ServerService _servers;

    public ServersController(ServerService servers)
    {
        _servers = servers;
    }

    // GET: /Servers
    public async Task<IActionResult> Index()
    {
        var servers = await _servers.GetAllAsync();
        return View(servers);
    }

    // GET: /Servers/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadGroups();

        return View(new ServerFormModel());
    }

    // POST: /Servers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServerFormModel model)
    {
        return await Save(model);
    }

    // GET: /Servers/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _servers.GetFormAsync(id);

        if (model is null)
            return NotFound();

        await LoadGroups();

        return View(model);
    }

    // POST: /Servers/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServerFormModel model)
    {
        return await Save(model);
    }

    // POST: /Servers/SetActive
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        var result = await _servers.SetActiveAsync(id, active);

        TempData[result.Ok ? "Ok" : "Error"] =
            result.Ok ? "Server updated." : result.Error;

        return RedirectToAction(nameof(Index));
    }

    // Shared save logic for Create and Edit.
    private async Task<IActionResult> Save(ServerFormModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _servers.SaveAsync(model);

            if (result.Ok)
            {
                TempData["Ok"] = "Server saved.";

                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", result.Error!);
        }

        await LoadGroups();

        return View(
            model.Id == 0 ? "Create" : "Edit",
            model
        );
    }

    // Load groups into the dropdown list.
    private async Task LoadGroups()
    {
        ViewBag.Groups = new SelectList(
            await _servers.GetGroupsAsync(),
            "Id",
            "Name"
        );
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id)
    {
        var result = await _servers.TestConnectionAsync(id);

        TempData[result.Ok ? "Ok" : "Error"] =
            result.Ok ? result.Info : result.Error;

        return RedirectToAction(nameof(Index));
    }
}
