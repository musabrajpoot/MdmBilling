using System.Data;
using MdmBilling.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace MdmBilling.Services.Udil;

public class UdilReader : IUdilReader
{
    private readonly UdilConnectionFactory _factory;
    private readonly UdilOptions _opt;
    private readonly ILogger<UdilReader> _log;

    public UdilReader(
        UdilConnectionFactory factory,
        IOptions<UdilOptions> options,
        ILogger<UdilReader> log)
    {
        _factory = factory;
        _opt = options.Value;
        _log = log;
    }

    public async Task<bool> TestConnectionAsync(
        Server server,
        CancellationToken ct)
    {
        try
        {
            await using var connection = _factory.Create(server);

            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(
                "SELECT 1",
                connection)
            {
                CommandTimeout = _opt.CommandTimeoutSeconds
            };

            await command.ExecuteScalarAsync(ct);

            return true;
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException)
        {
            _log.LogWarning(
                ex,
                "Connection test failed for {Server}",
                server.Name);

            return false;
        }
    }

    public async Task<ServerCheckResult> CheckMetersAsync(
        Server server,
        IReadOnlyList<string> meterNumbers,
        CancellationToken ct)
    {
        try
        {
            await using var connection = _factory.Create(server);

            await connection.OpenAsync(ct);

            // Find meters that exist on this server.
            var found = await FindMetersAsync(
                connection,
                meterNumbers,
                ct);

            // Get online/offline status.
            var online = await ReadOnlineStatusAsync(
                connection,
                found,
                ct);

            // Get daily data availability.
            var data = await ReadDailyDataAsync(
                connection,
                found,
                ct);

            var list = new List<MeterCheck>(
                meterNumbers.Count);

            foreach (var meter in meterNumbers)
            {
                if (!found.Contains(meter))
                {
                    list.Add(new MeterCheck(
                        meter,
                        false,
                        OnlineStatus.NotChecked,
                        DataStatus.NotChecked,
                        null));

                    continue;
                }

                var onlineStatus = online.TryGetValue(
                    meter,
                    out var os)
                    ? os
                    : OnlineStatus.Unknown;

                var dailyData = data.TryGetValue(
                    meter,
                    out var ds)
                    ? ds
                    : (DataStatus.Unknown, (string?)null);

                list.Add(new MeterCheck(
                    meter,
                    true,
                    onlineStatus,
                    dailyData.Item1,
                    dailyData.Item2));
            }

            return new ServerCheckResult(
                server.Id,
                true,
                null,
                list);
        }
        catch (Exception ex) when (
            ex is SqlException or TimeoutException)
        {
            _log.LogWarning(
                ex,
                "Server {Server} could not be read",
                server.Name);

            return new ServerCheckResult(
                server.Id,
                false,
                ex.Message,
                Array.Empty<MeterCheck>());
        }
    }

    // =========================================================
    // DBA QUERY 1
    // Find which supplied meter numbers exist on this server.
    // =========================================================

    private Task<HashSet<string>> FindMetersAsync(
        SqlConnection connection,
        IReadOnlyList<string> meters,
        CancellationToken ct)
    {
        throw new NotImplementedException(
            "Waiting for the DBA: meter deployment query.");
    }

    // =========================================================
    // DBA QUERY 2
    // Read online status and apply the agreed business rule.
    // =========================================================

    private Task<Dictionary<string, OnlineStatus>>
        ReadOnlineStatusAsync(
            SqlConnection connection,
            HashSet<string> found,
            CancellationToken ct)
    {
        throw new NotImplementedException(
            "Waiting for the DBA: online status query and rule.");
    }

    // =========================================================
    // DBA QUERY 3
    // Read daily data and apply the agreed missing-data rule.
    // =========================================================

    private Task<Dictionary<string, (
        DataStatus Status,
        string? Details)>>
        ReadDailyDataAsync(
            SqlConnection connection,
            HashSet<string> found,
            CancellationToken ct)
    {
        throw new NotImplementedException(
            "Waiting for the DBA: daily data query and rule.");
    }

    // Builds safe SQL parameter placeholders for an IN clause.
    // Never insert meter numbers directly into SQL text.
    private static (
        string Placeholders,
        SqlParameter[] Parameters)
        BuildInClause(IReadOnlyList<string> meters)
    {
        var parameters = meters
            .Select((meter, index) =>
                new SqlParameter(
                    $"@m{index}",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = meter
                })
            .ToArray();

        var placeholders = string.Join(
            ",",
            parameters.Select(p => p.ParameterName));

        return (placeholders, parameters);
    }
}