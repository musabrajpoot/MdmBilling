namespace MdmBilling.Models;
public static class AppRoles
{
public const string SuperAdmin = "SuperAdmin";
public const string OperationalUser = "OperationalUser";
public const string ReportViewer = "ReportViewer";
public const string Auditor = "Auditor";
public static readonly string[] All = { SuperAdmin, OperationalUser,
ReportViewer, Auditor };
// Role groups used in [Authorize(Roles = ...)]
public const string RunBatches = SuperAdmin + "," + OperationalUser;
public const string ViewResults = SuperAdmin + "," + OperationalUser +
"," + ReportViewer + "," + Auditor;
public const string DownloadReports = SuperAdmin + "," + OperationalUser
+ "," + ReportViewer;
public const string ViewLogs = SuperAdmin + "," + Auditor;
}
