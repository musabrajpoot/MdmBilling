namespace MdmBilling.Services.Udil;

public class UdilMeterServerLocator : IMeterServerLocator
{
    public Task<Dictionary<string, int>> FindServersAsync(
        IReadOnlyList<string> meterNumbers,
        CancellationToken ct)
    {
        throw new NotImplementedException(
            "Waiting for the DBA: where can the server of each meter be read? " +
            "(SRS question 4 and section 13)");
    }
}