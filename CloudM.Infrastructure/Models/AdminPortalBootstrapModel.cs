namespace CloudM.Infrastructure.Models
{
    public class AdminPortalBootstrapModel
    {
        public string PortalName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ApiNamespace { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public List<string> PlannedModules { get; set; } = new();
    }
}
