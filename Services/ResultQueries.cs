using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.ViewModels;
namespace MdmBilling.Services;

public static class ResultQueries
{
    public static IQueryable<MeterResultRow> MeterRows(this AppDbContext db,
    int runId) =>
    from map in db.MeterServerMappings
    where map.RunId == runId
    join st in db.MeterStatusResults on new
    {
        map.RunId,
        map.BatchMeterId
    } equals new { st.RunId, st.BatchMeterId }
    join da in db.DailyDataAvailabilities on new
    {
        map.RunId,
        map.BatchMeterId
    } equals new { da.RunId, da.BatchMeterId }
    select new MeterResultRow
    {
        MeterNumber = map.BatchMeter!.MeterNumber,
        ServerName = map.Server != null ? map.Server.Name : null,
        GroupName = map.Server != null && map.Server.Group != null ? map.Server.Group.Name : null,
        Found = map.Found,
        Online = st.OnlineStatus,
        Data = da.DataStatus,
        MissingDetails = da.MissingDetails,
        Remarks = st.Remarks
    };
    public static IQueryable<MeterResultRow> ApplyFilter(this
    IQueryable<MeterResultRow> q, string? filter) => filter switch
    {
        "online" => q.Where(r => r.Online == OnlineStatus.Online),
        "offline" => q.Where(r => r.Online == OnlineStatus.Offline),
        "dataAvailable" => q.Where(r => r.Data == DataStatus.Available),
        "dataMissing" => q.Where(r => r.Data == DataStatus.Missing),
        "notFound" => q.Where(r => !r.Found && r.Online ==
    OnlineStatus.Unknown),
        "notChecked" => q.Where(r => r.Online == OnlineStatus.NotChecked),
        "attention" => q.Where(r => !(r.Found && r.Online ==
    OnlineStatus.Online && r.Data == DataStatus.Available)),
        _ => q
    };
}