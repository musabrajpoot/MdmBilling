using Microsoft.AspNetCore.Identity;
namespace MdmBilling.Models;
public class ApplicationUser : IdentityUser
{
public string FullName { get; set; } = "";
public string? Office { get; set; }
public bool IsActive { get; set; } = true;
public bool CanDownloadReports { get; set; }
public bool MustChangePassword { get; set; }
public string? CreatedById { get; set; }
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
public DateTime? LastLoginAt { get; set; }
}
