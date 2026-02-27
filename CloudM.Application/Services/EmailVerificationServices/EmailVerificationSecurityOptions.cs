namespace CloudM.Application.Services.EmailVerificationServices
{
    public class EmailVerificationSecurityOptions
    {
        // otp validity duration in minutes.
        public int OtpExpiresMinutes { get; set; } = 5;
        // minimum wait time in seconds between otp send requests for the same email.
        public int ResendCooldownSeconds { get; set; } = 60;
        // maximum otp send requests allowed per email in one short window.
        public int MaxSendsPerWindow { get; set; } = 3;
        // short email send window size in minutes.
        public int SendWindowMinutes { get; set; } = 15;
        // maximum otp send requests allowed per email in a rolling 24-hour period.
        public int MaxSendsPerDay { get; set; } = 10;
        // maximum otp send requests allowed per ip in one short window.
        public int MaxSendsPerIpWindow { get; set; } = 10;
        // short ip send window size in minutes.
        public int IpSendWindowMinutes { get; set; } = 15;
        // maximum otp send requests allowed per ip in a rolling 24-hour period.
        public int MaxSendsPerIpDay { get; set; } = 200;
        // maximum otp send requests for one email+ip pair in one short window; set 0 to disable.
        public int MaxSendsPerEmailIpWindow { get; set; } = 0;
        // short email+ip send window size in minutes.
        public int EmailIpSendWindowMinutes { get; set; } = 15;
        // global cap of otp send requests across the whole system in one window.
        public int MaxGlobalSendsPerWindow { get; set; } = 1000;
        // global send window size in minutes.
        public int GlobalSendWindowMinutes { get; set; } = 60;
        // reserved legacy field for ip lock duration in minutes.
        public int IpLockMinutes { get; set; } = 15;
        // maximum invalid otp verification attempts before temporary lock.
        public int MaxFailedAttempts { get; set; } = 5;
        // temporary lock duration in minutes after reaching max failed attempts.
        public int LockMinutes { get; set; } = 15;
        // background cleanup interval in minutes for stale sql verification rows.
        public int CleanupIntervalMinutes { get; set; } = 30;
        // maximum row age in hours before stale verification records are cleaned up.
        public int RetentionHours { get; set; } = 24;
        // server-side secret used in otp hashing; should be unique per environment.
        public string OtpPepper { get; set; } = "CHANGE_ME_OTP_PEPPER";
    }
}
