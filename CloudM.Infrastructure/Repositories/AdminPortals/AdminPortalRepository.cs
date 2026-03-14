using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminPortals
{
    public class AdminPortalRepository : IAdminPortalRepository
    {
        public Task<AdminPortalBootstrapModel> GetBootstrapAsync()
        {
            var model = new AdminPortalBootstrapModel
            {
                PortalName = "CloudM Admin Portal",
                Status = "scaffolded",
                ApiNamespace = "api/admin/*",
                Controller = "AdminPortalController",
                Service = "AdminPortalService",
                Repository = "AdminPortalRepository",
                PlannedModules = new List<string>
                {
                    "auth-guard",
                    "shell-ui",
                    "account-status",
                    "moderation-center",
                    "report-center",
                    "audit-log"
                }
            };

            return Task.FromResult(model);
        }
    }
}
