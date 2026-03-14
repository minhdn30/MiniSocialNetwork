using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Infrastructure.Repositories.AdminPortals;

namespace CloudM.Application.Services.AdminPortalServices
{
    public class AdminPortalService : IAdminPortalService
    {
        private readonly IAdminPortalRepository _adminPortalRepository;

        public AdminPortalService(IAdminPortalRepository adminPortalRepository)
        {
            _adminPortalRepository = adminPortalRepository;
        }

        public async Task<AdminPortalBootstrapResponse> GetBootstrapAsync()
        {
            var model = await _adminPortalRepository.GetBootstrapAsync();

            return new AdminPortalBootstrapResponse
            {
                PortalName = model.PortalName,
                Status = model.Status,
                ApiNamespace = model.ApiNamespace,
                Controller = model.Controller,
                Service = model.Service,
                Repository = model.Repository,
                PlannedModules = model.PlannedModules,
                CheckedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            };
        }
    }
}
