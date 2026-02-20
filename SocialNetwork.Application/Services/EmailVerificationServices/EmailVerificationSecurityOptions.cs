namespace SocialNetwork.Application.Services.EmailVerificationServices
{
    public class EmailVerificationSecurityOptions
    {
        public int OtpExpiresMinutes { get; set; } = 5;
        public int ResendCooldownSeconds { get; set; } = 60;
        public int MaxSendsPerWindow { get; set; } = 3;
        public int SendWindowMinutes { get; set; } = 15;
        public int MaxSendsPerDay { get; set; } = 10;
        public int MaxSendsPerIpWindow { get; set; } = 10;
        public int IpSendWindowMinutes { get; set; } = 15;
        public int MaxSendsPerIpDay { get; set; } = 30;
        public int IpLockMinutes { get; set; } = 15;
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockMinutes { get; set; } = 15;
        public int CleanupIntervalMinutes { get; set; } = 30;
        public int RetentionHours { get; set; } = 24;
        public int Pbkdf2Iterations { get; set; } = 100000;
        public string OtpPepper { get; set; } = "CHANGE_ME_OTP_PEPPER";
    }
}
