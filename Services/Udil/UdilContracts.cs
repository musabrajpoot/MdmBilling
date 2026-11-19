using MdmBilling.Models;
namespace MdmBilling.Services.Udil;
public record MeterCheck(string MeterNumber, bool Found, OnlineStatus Online,
DataStatus Data, string? Remarks);
public record ServerCheckResult(int ServerId, bool Succeeded, string? Error,
IReadOnlyList<MeterCheck> Meters);
// Answers: on which server is each meter? Meters that are on no server are left out.   
public interface IMeterServerLocator
{
Task<Dictionary<string, int>> FindServersAsync(IReadOnlyList<string>
meterNumbers, CancellationToken ct);
}
// Reads one server. Read-only.
public interface IUdilReader
{
Task<bool> TestConnectionAsync(Server server, CancellationToken ct);
Task<ServerCheckResult> CheckMetersAsync(Server server,
IReadOnlyList<string> meterNumbers, CancellationToken ct);
}