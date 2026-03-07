namespace CloudM.Application.Services.FollowServices
{
    public class FollowAutoAcceptOptions
    {
        public bool EnableWorker { get; set; } = true;
        public int WorkerPollIntervalMs { get; set; } = 1000;
        public int BatchSize { get; set; } = 200;

        public FollowAutoAcceptOptions Normalize()
        {
            WorkerPollIntervalMs = WorkerPollIntervalMs <= 0 ? 1000 : WorkerPollIntervalMs;
            BatchSize = BatchSize <= 0 ? 200 : BatchSize;
            return this;
        }
    }
}
