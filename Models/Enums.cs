namespace MdmBilling.Models;
public enum RunStatus { Queued, Running, Completed, CompletedWithErrors,
Failed, Interrupted }
public enum OnlineStatus { Unknown, Online, Offline, NotChecked }
public enum DataStatus { Unknown, Available, Missing, NotChecked }
public enum ServerRunState { Pending, Succeeded, Failed }
public enum ReportFormat { Excel, Pdf }
