
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Services;

public class ServerService
{
    private readonly AppDbContext _db;
    private readonly CredentialProtector _protector;
    private readonly AuditService _audit;
    private readonly Udil.IUdilReader _udil;

    public ServerService(
        AppDbContext db,
        CredentialProtector protector,
        AuditService audit,
          Udil.IUdilReader udil)
    {
        _db = db;
        _protector = protector;
        _audit = audit;
        _udil = udil;
    }

    // Get all servers with their groups and connections.
    public Task<List<Server>> GetAllAsync()
    {
        return _db.Servers
            .AsNoTracking()
            .Include(s => s.Group)
            .Include(s => s.Connections)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    // Get available server groups.
    public Task<List<ServerGroup>> GetGroupsAsync()
    {
        return _db.ServerGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    // Get server details for editing.
    public async Task<ServerFormModel?> GetFormAsync(int id)
    {
        var server = await _db.Servers
            .AsNoTracking()
            .Include(s => s.Connections)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (server is null)
            return null;

        var connection = server.Connections.FirstOrDefault();

        return new ServerFormModel
        {
            Id = server.Id,
            GroupId = server.GroupId,
            Name = server.Name,
            IpAddress = server.IpAddress,

            DbEngine = connection?.DbEngine ?? "",
            Host = connection?.Host ?? "",
            Port = connection?.Port ?? 0,
            DatabaseName = connection?.DatabaseName ?? "",
            Username = connection?.Username ?? ""
        };
    }

    // Create or update a server.
    public async Task<ServiceResult> SaveAsync(ServerFormModel model)
    {
        var name = model.Name.Trim();

        // Prevent duplicate server names.
        if (await _db.Servers.AnyAsync(
            s => s.Name == name && s.Id != model.Id))
        {
            return ServiceResult.Fail(
                "A server with this name already exists."
            );
        }

        Server server;

        if (model.Id == 0)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                return ServiceResult.Fail(
                    "Password is required for a new server."
                );
            }

            server = new Server
            {
                IsActive = true
            };

            _db.Servers.Add(server);
        }
        else
        {
            var existing = await _db.Servers
                .Include(s => s.Connections)
                .FirstOrDefaultAsync(s => s.Id == model.Id);

            if (existing is null)
            {
                return ServiceResult.Fail("Server not found.");
            }

            server = existing;
        }

        // Get existing connection or create a new one.
        var connection = server.Connections.FirstOrDefault();

        if (connection is null)
        {
            connection = new DatabaseConnection();
            server.Connections.Add(connection);
        }

        // Save server details.
        server.GroupId = model.GroupId;
        server.Name = name;
        server.IpAddress = model.IpAddress.Trim();

        // Save database connection details.
        connection.DbEngine = model.DbEngine.Trim();
        connection.Host = model.Host.Trim();
        connection.Port = model.Port;
        connection.DatabaseName = model.DatabaseName.Trim();
        connection.Username = model.Username.Trim();

        // Encrypt password only when a new password is provided.
        // A blank password during editing keeps the old password.
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            connection.EncryptedPassword =
                _protector.Protect(model.Password);
        }

        await _db.SaveChangesAsync();

        // Write audit log.
        await _audit.LogAsync(
            model.Id == 0 ? "ServerCreated" : "ServerUpdated",
            "Server",
            server.Id.ToString()
        );

        return ServiceResult.Success();
    }

    // Activate or deactivate a server.
    public async Task<ServiceResult> SetActiveAsync(
        int id,
        bool active)
    {
        var server = await _db.Servers
            .FirstOrDefaultAsync(s => s.Id == id);

        if (server is null)
        {
            return ServiceResult.Fail("Server not found.");
        }

        server.IsActive = active;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            active ? "ServerActivated" : "ServerDeactivated",
            "Server",
            id.ToString()
        );

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> TestConnectionAsync(
    int id,
    CancellationToken ct = default)
    {
        var server = await _db.Servers
            .AsNoTracking()
            .Include(s => s.Connections)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (server is null)
        {
            return ServiceResult.Fail("Server not found.");
        }

        var ok = await _udil.TestConnectionAsync(server, ct);

        await _audit.LogAsync(
            ok ? "ServerTestOk" : "ServerTestFailed",
            "Server",
            id.ToString());

        return ok
            ? ServiceResult.Success(
                $"{server.Name}: connection OK.")
            : ServiceResult.Fail(
                $"{server.Name}: connection failed. See the logs.");
    }
}
