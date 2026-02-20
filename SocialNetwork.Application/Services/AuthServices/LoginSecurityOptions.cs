namespace SocialNetwork.Application.Services.AuthServices
{
    public class LoginSecurityOptions
    {
        public int MaxFailedAttemptsPerEmailWindow { get; set; } = 10;
        public int EmailWindowMinutes { get; set; } = 15;
        public int MaxFailedAttemptsPerIpWindow { get; set; } = 50;
        public int IpWindowMinutes { get; set; } = 15;
        public int MaxFailedAttemptsPerEmailIpWindow { get; set; } = 5;
        public int EmailIpWindowMinutes { get; set; } = 15;
        public int LockMinutes { get; set; } = 15;
    }
}
