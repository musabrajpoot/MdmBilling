using MdmBilling.Models;
using MdmBilling.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace MdmBilling.Services.Udil;

public class UdilConnectionFactory
{
    private readonly CredentialProtector _protector;
    private readonly UdilOptions _opt;

    public UdilConnectionFactory(
        CredentialProtector protector,
        IOptions<UdilOptions> options)
    {
        _protector = protector;
        _opt = options.Value;
    }

    // The server must be loaded with its Connections.
    public SqlConnection Create(Server server)
    {
        var connection = server.Connections
            .FirstOrDefault(x => x.IsActive)
            ?? throw new InvalidOperationException(
                $"Server {server.Name} has no active connection.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = connection.Port > 0
                ? $"{connection.Host},{connection.Port}"
                : connection.Host,

            InitialCatalog = connection.DatabaseName,

            UserID = connection.Username,

            Password = _protector.Unprotect(
                connection.EncryptedPassword),

            ConnectTimeout = _opt.ConnectTimeoutSeconds,

            Encrypt = _opt.Encrypt,

            TrustServerCertificate = _opt.TrustServerCertificate,

            ApplicationName = "MdmBillingMonitor",

            // This is a hint only.
            // The read-only database login provides the real protection.
            ApplicationIntent = ApplicationIntent.ReadOnly
        };

        return new SqlConnection(builder.ConnectionString);
    }
}