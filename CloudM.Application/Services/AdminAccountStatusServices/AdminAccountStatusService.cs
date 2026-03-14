using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AdminAccountStatuses;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AdminAccountStatusServices
{
    public class AdminAccountStatusService : IAdminAccountStatusService
    {
        private const int MaxReasonLength = 300;

        private readonly IAdminAccountStatusRepository _adminAccountStatusRepository;
        private readonly IAdminAuditLogService _adminAuditLogService;
        private readonly IUnitOfWork _unitOfWork;

        public AdminAccountStatusService(
            IAdminAccountStatusRepository adminAccountStatusRepository,
            IAdminAuditLogService adminAuditLogService,
            IUnitOfWork unitOfWork)
        {
            _adminAccountStatusRepository = adminAccountStatusRepository;
            _adminAuditLogService = adminAuditLogService;
            _unitOfWork = unitOfWork;
        }

        public async Task<AdminAccountStatusUpdateResponse> UpdateStatusAsync(
            Guid adminId,
            Guid accountId,
            AdminAccountStatusUpdateRequest request,
            string? requesterIpAddress)
        {
            if (!Enum.IsDefined(typeof(AccountStatusEnum), request.Status))
            {
                throw new BadRequestException("Invalid account status.");
            }

            var normalizedReason = NormalizeReason(request.Reason);
            if (string.IsNullOrWhiteSpace(normalizedReason))
            {
                throw new BadRequestException("Reason is required.");
            }

            var account = await _adminAccountStatusRepository.GetTrackedAccountByIdAsync(accountId);
            if (account == null)
            {
                throw new NotFoundException($"Account with ID {accountId} not found.");
            }

            if (account.Status == request.Status)
            {
                throw new BadRequestException("This account already has the selected status.");
            }

            var previousStatus = account.Status;
            account.Status = request.Status;
            account.UpdatedAt = DateTime.UtcNow;
            await _adminAccountStatusRepository.UpdateAsync(account);

            await _adminAuditLogService.RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = adminId,
                Module = "accounts",
                ActionType = BuildAccountStatusActionType(request.Status),
                TargetType = "Account",
                TargetId = account.AccountId.ToString(),
                Summary = normalizedReason,
                RequestIp = requesterIpAddress,
            });
            await _unitOfWork.CommitAsync();

            return new AdminAccountStatusUpdateResponse
            {
                AccountId = account.AccountId,
                Username = account.Username,
                Fullname = account.FullName,
                Email = account.Email,
                Role = account.Role?.RoleName ?? string.Empty,
                PreviousStatus = previousStatus.ToString(),
                CurrentStatus = account.Status.ToString(),
                UpdatedAt = account.UpdatedAt ?? DateTime.UtcNow,
                Reason = normalizedReason,
                RequiresSignOut = adminId == account.AccountId && account.Status != AccountStatusEnum.Active,
            };
        }

        private static string NormalizeReason(string reason)
        {
            var normalizedReason = (reason ?? string.Empty).Trim();
            if (normalizedReason.Length <= MaxReasonLength)
            {
                return normalizedReason;
            }

            return normalizedReason[..MaxReasonLength];
        }

        private static string BuildAccountStatusActionType(AccountStatusEnum status)
        {
            return status switch
            {
                AccountStatusEnum.Active => "AccountStatusSetActive",
                AccountStatusEnum.Inactive => "AccountStatusSetInactive",
                AccountStatusEnum.Suspended => "AccountStatusSetSuspended",
                AccountStatusEnum.Banned => "AccountStatusSetBanned",
                AccountStatusEnum.Deleted => "AccountStatusSetDeleted",
                AccountStatusEnum.EmailNotVerified => "AccountStatusSetEmailNotVerified",
                _ => "AccountStatusUpdated"
            };
        }
    }
}
