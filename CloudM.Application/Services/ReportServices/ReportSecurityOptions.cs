namespace CloudM.Application.Services.ReportServices
{
    public class ReportSecurityOptions
    {
        // maximum accepted reports per account in one short window.
        public int MaxReportsPerAccountWindow { get; set; } = 8;
        // maximum accepted reports per ip in one short window.
        public int MaxReportsPerIpWindow { get; set; } = 24;
        // shared rate-limit window size in minutes.
        public int WindowMinutes { get; set; } = 10;
    }
}
