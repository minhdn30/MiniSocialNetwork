namespace CloudM.Application.Services.FollowServices
{
    public interface IFollowAutoAcceptService
    {
        Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}
