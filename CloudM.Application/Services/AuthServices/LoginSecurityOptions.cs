namespace CloudM.Application.Services.AuthServices
{
    public class LoginSecurityOptions
    {
        // maximum failed login attempts per email in one short window.
        public int MaxFailedAttemptsPerEmailWindow { get; set; } = 10;
        // email-based failed-login window size in minutes.
        public int EmailWindowMinutes { get; set; } = 15;
        // maximum failed login attempts per ip in one short window.
        public int MaxFailedAttemptsPerIpWindow { get; set; } = 50;
        // ip-based failed-login window size in minutes.
        public int IpWindowMinutes { get; set; } = 15;
        // maximum failed login attempts per email+ip pair in one short window; set 0 to disable.
        public int MaxFailedAttemptsPerEmailIpWindow { get; set; } = 5;
        // email+ip failed-login window size in minutes.
        public int EmailIpWindowMinutes { get; set; } = 15;
        // lock duration in minutes after exceeding any configured failed-attempt threshold.
        public int LockMinutes { get; set; } = 15;
    }
}
