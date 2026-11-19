using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
namespace MdmBilling.Controllers;
public abstract class BaseController : Controller
{
protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
?? "";
}
