using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Application.Services.AdminAuditLogServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AdminModerations;
using CloudM.Infrastructure.Repositories.AdminReports;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AdminReportServices
{
    public class AdminReportService : IAdminReportService
    {
        private const int MaxReasonCodeLength = 100;
        private const int MaxDetailLength = 1000;
        private const int DefaultLimit = 12;
        private const int MaxLimit = 30;

        private readonly IAdminModerationRepository _adminModerationRepository;
        private readonly IAdminReportRepository _adminReportRepository;
        private readonly IAdminAuditLogService _adminAuditLogService;
        private readonly IUnitOfWork _unitOfWork;

        public AdminReportService(
            IAdminModerationRepository adminModerationRepository,
            IAdminReportRepository adminReportRepository,
            IAdminAuditLogService adminAuditLogService,
            IUnitOfWork unitOfWork)
        {
            _adminModerationRepository = adminModerationRepository;
            _adminReportRepository = adminReportRepository;
            _adminAuditLogService = adminAuditLogService;
            _unitOfWork = unitOfWork;
        }

        public async Task<AdminReportItemResponse> CreateInternalReportAsync(
            Guid adminId,
            AdminReportCreateRequest request,
            string? requesterIpAddress)
        {
            var targetType = ParseTargetType(request.TargetType);
            if (!await _adminModerationRepository.TargetExistsAsync(targetType, request.TargetId))
            {
                throw new NotFoundException("This moderation target does not exist.");
            }

            var normalizedReasonCode = NormalizeReasonCode(request.ReasonCode);
            if (string.IsNullOrWhiteSpace(normalizedReasonCode))
            {
                throw new BadRequestException("Reason code is required.");
            }

            var normalizedDetail = NormalizeDetail(request.Detail);
            var report = new ModerationReport
            {
                TargetType = targetType,
                TargetId = request.TargetId,
                ReasonCode = normalizedReasonCode,
                Detail = normalizedDetail,
                Status = ModerationReportStatusEnum.Open,
                SourceType = ModerationReportSourceEnum.AdminInternal,
                CreatedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow,
            };

            var action = new ModerationReportAction
            {
                ModerationReportId = report.ModerationReportId,
                AdminId = adminId,
                ActionType = ModerationReportActionTypeEnum.CreateInternal,
                ToStatus = ModerationReportStatusEnum.Open,
                Note = normalizedDetail,
                CreatedAt = DateTime.UtcNow,
            };

            await _adminReportRepository.AddAsync(report, action);

            await _adminAuditLogService.RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = adminId,
                Module = "reports",
                ActionType = "ReportCreatedInternal",
                TargetType = targetType.ToString(),
                TargetId = request.TargetId.ToString(),
                Summary = normalizedReasonCode,
                RequestIp = requesterIpAddress,
            });
            await _unitOfWork.CommitAsync();

            return new AdminReportItemResponse
            {
                ModerationReportId = report.ModerationReportId,
                TargetType = report.TargetType.ToString(),
                TargetId = report.TargetId,
                ReasonCode = report.ReasonCode,
                Detail = report.Detail,
                Status = report.Status.ToString(),
                SourceType = report.SourceType.ToString(),
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                ResolvedAt = report.ResolvedAt,
            };
        }

        public async Task<AdminReportListResponse> GetRecentReportsAsync(AdminReportListRequest request)
        {
            var normalizedLimit = NormalizeLimit(request.Limit);
            var status = ParseOptionalStatus(request.Status);
            var targetType = ParseOptionalTargetType(request.TargetType);
            var items = await _adminReportRepository.GetRecentAsync(status, targetType, normalizedLimit);

            return new AdminReportListResponse
            {
                Status = status?.ToString() ?? string.Empty,
                TargetType = targetType?.ToString() ?? string.Empty,
                AppliedLimit = normalizedLimit,
                TotalResults = items.Count,
                Items = items.Select(item => new AdminReportItemResponse
                {
                    ModerationReportId = item.ModerationReportId,
                    TargetType = item.TargetType.ToString(),
                    TargetId = item.TargetId,
                    ReasonCode = item.ReasonCode,
                    Detail = item.Detail,
                    Status = item.Status.ToString(),
                    SourceType = item.SourceType.ToString(),
                    CreatedByAdminEmail = item.CreatedByAdminEmail,
                    CreatedByAdminFullname = item.CreatedByAdminFullname,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    ResolvedAt = item.ResolvedAt,
                }).ToList(),
            };
        }

        public async Task<AdminReportItemResponse> UpdateStatusAsync(
            Guid adminId,
            Guid moderationReportId,
            AdminReportStatusUpdateRequest request,
            string? requesterIpAddress)
        {
            var report = await _adminReportRepository.GetTrackedByIdAsync(moderationReportId);
            if (report == null)
            {
                throw new NotFoundException($"Report with ID {moderationReportId} not found.");
            }

            var targetStatus = ParseStatus(request.Status);
            if (report.Status == targetStatus)
            {
                throw new BadRequestException("This report already has the selected status.");
            }

            var normalizedNote = NormalizeDetail(request.Note);
            var previousStatus = report.Status;
            report.Status = targetStatus;
            report.UpdatedAt = DateTime.UtcNow;
            if (targetStatus == ModerationReportStatusEnum.Resolved || targetStatus == ModerationReportStatusEnum.Dismissed)
            {
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolvedByAdminId = adminId;
            }
            else
            {
                report.ResolvedAt = null;
                report.ResolvedByAdminId = null;
            }

            await _adminReportRepository.AddActionAsync(new ModerationReportAction
            {
                ModerationReportId = report.ModerationReportId,
                AdminId = adminId,
                ActionType = BuildActionType(targetStatus),
                FromStatus = previousStatus,
                ToStatus = targetStatus,
                Note = normalizedNote,
                CreatedAt = DateTime.UtcNow,
            });

            await _adminAuditLogService.RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = adminId,
                Module = "reports",
                ActionType = BuildAuditActionType(targetStatus),
                TargetType = report.TargetType.ToString(),
                TargetId = report.TargetId.ToString(),
                Summary = string.IsNullOrWhiteSpace(normalizedNote) ? report.ReasonCode : normalizedNote,
                RequestIp = requesterIpAddress,
            });
            await _unitOfWork.CommitAsync();

            return new AdminReportItemResponse
            {
                ModerationReportId = report.ModerationReportId,
                TargetType = report.TargetType.ToString(),
                TargetId = report.TargetId,
                ReasonCode = report.ReasonCode,
                Detail = report.Detail,
                Status = report.Status.ToString(),
                SourceType = report.SourceType.ToString(),
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                ResolvedAt = report.ResolvedAt,
            };
        }

        private static ModerationTargetTypeEnum ParseTargetType(string targetType)
        {
            if (Enum.TryParse<ModerationTargetTypeEnum>((targetType ?? string.Empty).Trim(), true, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException("Invalid report target type.");
        }

        private static ModerationTargetTypeEnum? ParseOptionalTargetType(string targetType)
        {
            var normalizedTargetType = (targetType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTargetType))
            {
                return null;
            }

            return ParseTargetType(normalizedTargetType);
        }

        private static ModerationReportStatusEnum ParseStatus(string status)
        {
            if (Enum.TryParse<ModerationReportStatusEnum>((status ?? string.Empty).Trim(), true, out var parsed))
            {
                return parsed;
            }

            throw new BadRequestException("Invalid report status.");
        }

        private static ModerationReportStatusEnum? ParseOptionalStatus(string status)
        {
            var normalizedStatus = (status ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedStatus))
            {
                return null;
            }

            return ParseStatus(normalizedStatus);
        }

        private static string NormalizeReasonCode(string reasonCode)
        {
            var normalizedReasonCode = (reasonCode ?? string.Empty).Trim();
            if (normalizedReasonCode.Length <= MaxReasonCodeLength)
            {
                return normalizedReasonCode;
            }

            return normalizedReasonCode[..MaxReasonCodeLength];
        }

        private static string NormalizeDetail(string detail)
        {
            var normalizedDetail = (detail ?? string.Empty).Trim();
            if (normalizedDetail.Length <= MaxDetailLength)
            {
                return normalizedDetail;
            }

            return normalizedDetail[..MaxDetailLength];
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultLimit;
            }

            return Math.Clamp(limit, 1, MaxLimit);
        }

        private static ModerationReportActionTypeEnum BuildActionType(ModerationReportStatusEnum targetStatus)
        {
            return targetStatus switch
            {
                ModerationReportStatusEnum.Open => ModerationReportActionTypeEnum.Reopen,
                ModerationReportStatusEnum.InReview => ModerationReportActionTypeEnum.MoveToInReview,
                ModerationReportStatusEnum.Resolved => ModerationReportActionTypeEnum.Resolve,
                ModerationReportStatusEnum.Dismissed => ModerationReportActionTypeEnum.Dismiss,
                _ => ModerationReportActionTypeEnum.MoveToInReview
            };
        }

        private static string BuildAuditActionType(ModerationReportStatusEnum targetStatus)
        {
            return targetStatus switch
            {
                ModerationReportStatusEnum.Open => "ReportReopened",
                ModerationReportStatusEnum.InReview => "ReportMovedToInReview",
                ModerationReportStatusEnum.Resolved => "ReportResolved",
                ModerationReportStatusEnum.Dismissed => "ReportDismissed",
                _ => "ReportStatusUpdated"
            };
        }
    }
}
