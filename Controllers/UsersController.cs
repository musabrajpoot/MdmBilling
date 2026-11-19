using MdmBilling.Models;
using MdmBilling.Services;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MdmBilling.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class UsersController : BaseController
{
    private readonly UserService _users;

    public UsersController(UserService users) => _users = users;

    // Full address of the login page, used in the email.
    private string LoginUrl => Url.Action("Login", "Account", null, Request.Scheme) ?? "";

    public async Task<IActionResult> Index(string? search, string? role)
    {
        ViewBag.Search = search;
        ViewBag.Roles = new SelectList(AppRoles.All, role);
        return View(await _users.GetUsersAsync(search, role));
    }

    [HttpGet]
    public IActionResult Create()
    {
        LoadRoles(AppRoles.OperationalUser);
        return View(new UserFormModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _users.CreateAsync(model, UserId, LoginUrl);
            if (result.Ok)
                return Done(result, "User created." + (result.Warning is null ? " The login details were sent by email." : ""));

            ModelState.AddModelError("", result.Error!);
        }
        LoadRoles(model.Role);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var model = await _users.GetForEditAsync(id);
        if (model is null) return NotFound();
        LoadRoles(model.Role);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _users.UpdateAsync(model);
            if (result.Ok) return Done(result, "User saved.");
            ModelState.AddModelError("", result.Error!);
        }
        LoadRoles(model.Role);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id) =>
        Done(await _users.SetActiveAsync(id, false, UserId), "User deactivated.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id) =>
        Done(await _users.SetActiveAsync(id, true, UserId), "User activated.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var result = await _users.ResetPasswordAsync(id, LoginUrl);
        return Done(result, "Password reset." + (result.Ok && result.Warning is null ? " The new password was sent by email." : ""));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id) =>
        Done(await _users.UnlockAsync(id), "Account unlocked.");

    private IActionResult Done(ServiceResult result, string message)
    {
        if (!result.Ok)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Ok"] = message;
            if (result.Warning is not null)
            {
                // The email failed: show the temporary password so the admin can give it to the user.
                TempData["Error"] = result.Warning +
                    (result.Info is null ? "" : $" Give this temporary password to the user: {result.Info}");
            }
        }
        return RedirectToAction(nameof(Index));
    }

    private void LoadRoles(string selected) => ViewBag.Roles = new SelectList(AppRoles.All, selected);
}