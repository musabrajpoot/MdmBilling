using System.Security.Cryptography;
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Services;

public class UserService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly EmailService _email;

    public UserService(UserManager<ApplicationUser> users, AppDbContext db, AuditService audit, EmailService email)
    {
        _users = users;
        _db = db;
        _audit = audit;
        _email = email;
    }

    public async Task<List<UserListItem>> GetUsersAsync(string? search, string? role)
    {
        var q = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || (u.Email != null && u.Email.Contains(s)));
        }

        var roleMap = await (from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             select new { ur.UserId, RoleName = r.Name }).ToListAsync();
        var users = await q.OrderBy(u => u.FullName).ToListAsync();
        var now = DateTimeOffset.UtcNow;

        var list = users.Select(u => new UserListItem
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? "",
            Role = roleMap.FirstOrDefault(x => x.UserId == u.Id)?.RoleName ?? "",
            IsActive = u.IsActive,
            IsLocked = u.LockoutEnd != null && u.LockoutEnd > now,
            CanDownloadReports = u.CanDownloadReports,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        if (!string.IsNullOrWhiteSpace(role)) list = list.Where(x => x.Role == role).ToList();
        return list;
    }

    public async Task<UserFormModel?> GetForEditAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return null;
        var roles = await _users.GetRolesAsync(user);
        return new UserFormModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Office = user.Office,
            Phone = user.PhoneNumber,
            Role = roles.FirstOrDefault() ?? AppRoles.ReportViewer,
            CanDownloadReports = user.CanDownloadReports
        };
    }

    // Creates the user and emails the login details.
    public async Task<ServiceResult> CreateAsync(UserFormModel m, string actorId, string loginUrl)
    {
        if (!AppRoles.All.Contains(m.Role)) return ServiceResult.Fail("Select a valid role.");

        var email = m.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
            return ServiceResult.Fail("A user with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = m.FullName.Trim(),
            Office = m.Office?.Trim(),
            PhoneNumber = m.Phone?.Trim(),
            CanDownloadReports = m.CanDownloadReports,
            IsActive = true,
            MustChangePassword = true,
            CreatedById = actorId
        };

        var temp = NewTempPassword();
        var created = await _users.CreateAsync(user, temp);
        if (!created.Succeeded)
            return ServiceResult.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, m.Role);
        await _audit.LogAsync("UserCreated", "User", user.Id);

        var mail = await _email.SendCredentialsAsync(email, user.FullName, loginUrl, temp, isReset: false);
        await _audit.LogAsync(mail.Ok ? "CredentialsEmailed" : "CredentialsEmailFailed", "User", user.Id);

        // Show the password on screen only if the email could not be sent.
        return mail.Ok
            ? ServiceResult.Success()
            : ServiceResult.Success(info: temp, warning: $"The user was created, but the email could not be sent: {mail.Error}");
    }

    public async Task<ServiceResult> UpdateAsync(UserFormModel m)
    {
        if (m.Id is null) return ServiceResult.Fail("User not found.");
        var user = await _users.FindByIdAsync(m.Id);
        if (user is null) return ServiceResult.Fail("User not found.");
        if (!AppRoles.All.Contains(m.Role)) return ServiceResult.Fail("Select a valid role.");

        var currentRoles = await _users.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault();
        var roleChanged = oldRole != m.Role;

        if (roleChanged)
        {
            if (oldRole == AppRoles.SuperAdmin && await IsLastActiveSuperAdminAsync(user))
                return ServiceResult.Fail("The last active Super Admin cannot be changed to another role.");

            if (currentRoles.Count > 0) await _users.RemoveFromRolesAsync(user, currentRoles);
            await _users.AddToRoleAsync(user, m.Role);
        }

        user.FullName = m.FullName.Trim();
        user.Office = m.Office?.Trim();
        user.PhoneNumber = m.Phone?.Trim();
        user.CanDownloadReports = m.CanDownloadReports;
        await _users.UpdateAsync(user);

        await _audit.LogAsync(roleChanged ? "UserRoleChanged" : "UserUpdated", "User", user.Id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetActiveAsync(string id, bool active, string actorId)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("User not found.");

        if (!active)
        {
            if (user.Id == actorId) return ServiceResult.Fail("You cannot deactivate your own account.");
            if (await IsLastActiveSuperAdminAsync(user))
                return ServiceResult.Fail("The last active Super Admin cannot be deactivated.");
        }

        user.IsActive = active;
        await _users.UpdateAsync(user);
        await _audit.LogAsync(active ? "UserActivated" : "UserDeactivated", "User", user.Id);
        return ServiceResult.Success();
    }

    // Resets the password and emails the new temporary password.
    public async Task<ServiceResult> ResetPasswordAsync(string id, string loginUrl)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("User not found.");

        var temp = NewTempPassword();
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, temp);
        if (!result.Succeeded)
            return ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = true;
        await _users.UpdateAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync("PasswordReset", "User", user.Id);

        var mail = await _email.SendCredentialsAsync(user.Email ?? "", user.FullName, loginUrl, temp, isReset: true);
        await _audit.LogAsync(mail.Ok ? "CredentialsEmailed" : "CredentialsEmailFailed", "User", user.Id);

        return mail.Ok
            ? ServiceResult.Success()
            : ServiceResult.Success(info: temp, warning: $"The password was reset, but the email could not be sent: {mail.Error}");
    }

    public async Task<ServiceResult> UnlockAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("User not found.");

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync("UserUnlocked", "User", user.Id);
        return ServiceResult.Success();
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(ApplicationUser user)
    {
        if (!user.IsActive || !await _users.IsInRoleAsync(user, AppRoles.SuperAdmin)) return false;
        var admins = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        return admins.Count(a => a.IsActive) <= 1;
    }

    private static string NewTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%*?";
        const string all = upper + lower + digits + symbols;

        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(symbols) };
        while (chars.Count < 14) chars.Add(Pick(all));
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private static char Pick(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
}