using MdmBilling.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MdmBilling.Services;

public class UserStatusFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    public UserStatusFilter(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }
    public async Task OnActionExecutionAsync(ActionExecutingContext context,
    ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var user = await _users.GetUserAsync(context.HttpContext.User);
            if (user is null || !user.IsActive)
            {
                await _signIn.SignOutAsync();
                context.Result = new RedirectToActionResult("Login",
                "Account", null);
                return;
            }
            var controller =
            context.RouteData.Values["controller"]?.ToString();
            if (user.MustChangePassword && !string.Equals(controller,
            "Account", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("ChangePassword",
                "Account", null);
                return;
            }
        }
        await next();
    }
}
