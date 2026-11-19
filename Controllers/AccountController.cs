using MdmBilling.Models;
using MdmBilling.Services;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace MdmBilling.Controllers;

public class AccountController : BaseController
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AuditService _audit;
    public AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, AuditService audit)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
    }
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string?
    returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }
        var result = await _signIn.PasswordSignInAsync(user, model.Password,
        model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            await _audit.LogAsync("Login", "User", user.Id, user.Id);
            if (user.MustChangePassword) return
            RedirectToAction(nameof(ChangePassword));
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) :
            RedirectToAction("Index", "Home");
        }
        await _audit.LogAsync("LoginFailed", "User", user.Id, user.Id);
        ModelState.AddModelError("", result.IsLockedOut
        ? "Account is locked. Try again later."
        : "Invalid email or password.");
        return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [HttpGet]
    public IActionResult ChangePassword() => View(new
    ChangePasswordViewModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel
    model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        var result = await _users.ChangePasswordAsync(user,
        model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("",
            e.Description);
            return View(model);
        }
        user.MustChangePassword = false;
        await _users.UpdateAsync(user);
        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync("PasswordChanged", "User", user.Id);
        TempData["Ok"] = "Password changed.";
        return RedirectToAction("Index", "Home");
    }
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

