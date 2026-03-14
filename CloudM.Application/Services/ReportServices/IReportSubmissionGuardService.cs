namespace CloudM.Application.Services.ReportServices
{
    public interface IReportSubmissionGuardService
    {
        Task EnforceSubmissionAllowedAsync(Guid accountId, string? ipAddress, DateTime nowUtc);
        Task RecordSubmissionAsync(Guid accountId, string? ipAddress, DateTime nowUtc);
    }
}
