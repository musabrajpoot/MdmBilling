using System.ComponentModel.DataAnnotations;
using MdmBilling.Models;
namespace MdmBilling.ViewModels;
// Simple result used by services (also used in Phase 3).
// public class ServiceResult
// {
//     public bool Ok { get; init; }
//     public string? Error { get; init; }
//     public string? Info { get; init; }
//     public string? Warning { get; init; }

//     public static ServiceResult Success(string? info = null, string warning = null) => new()
//     {
//         Ok =
//     true,
//         Info = info
//     };
//     public static ServiceResult Fail(string error) => new()
//     {
//         Ok = false,
//         Error = error
//     };
// }
public class ServiceResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Info { get; init; }
    public string? Warning { get; init; }

    public static ServiceResult Success(string? info = null, string? warning = null) =>
        new() { Ok = true, Info = info, Warning = warning };

    public static ServiceResult Fail(string error) => new() { Ok = false, Error = error };
}
public class UserFormModel
{
    public string? Id { get; set; }
    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = "";
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [StringLength(100), Display(Name = "Office or department")]
    public string? Office { get; set; }
    [Phone]
    public string? Phone { get; set; }
    [Required]
    public string Role { get; set; } = AppRoles.OperationalUser;
    [Display(Name = "Can download reports")]
    public bool CanDownloadReports { get; set; }
}
public class UserListItem
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public bool CanDownloadReports { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

