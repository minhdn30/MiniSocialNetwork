using CloudM.Application.DTOs.ReportDTOs;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Reports;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.ReportServices
{
    public class ReportService : IReportService
    {
        private const int MaxReasonCodeLength = 100;
        private const int MaxDetailLength = 1000;

        private readonly IReportRepository _reportRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ReportService(
            IReportRepository reportRepository,
            IUnitOfWork unitOfWork)
        {
            _reportRepository = reportRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ReportCreateResponse> CreateReportAsync(Guid currentId, ReportCreateRequest request, string? requesterIpAddress)
        {
            var nowUtc = DateTime.UtcNow;
            var targetType = ParseTargetType(request.TargetType);
            if (request.TargetId == Guid.Empty)
            {
                throw new BadRequestException("Target ID is required.");
            }

            var normalizedReasonCode = NormalizeReasonCode(request.ReasonCode);
            if (string.IsNullOrWhiteSpace(normalizedReasonCode))
            {
                throw new BadRequestException("Reason code is required.");
            }

            if (!await _reportRepository.CanSubmitReportAsync(currentId, targetType, request.TargetId))
            {
                throw new NotFoundException("This report target does not exist.");
            }

            if (await _reportRepository.HasPendingDuplicateAsync(currentId, targetType, request.TargetId))
            {
                throw new BadRequestException("You already have an open report for this item.");
            }

            var report = new ModerationReport
            {
                TargetType = targetType,
                TargetId = request.TargetId,
                ReasonCode = normalizedReasonCode,
                Detail = NormalizeDetail(request.Detail),
                Status = ModerationReportStatusEnum.Open,
                SourceType = ModerationReportSourceEnum.UserSubmitted,
                ReporterAccountId = currentId,
                CreatedAt = nowUtc,
            };

            await _reportRepository.AddAsync(report);
            await _unitOfWork.CommitAsync();

            return new ReportCreateResponse
            {
                ModerationReportId = report.ModerationReportId,
                TargetType = report.TargetType.ToString(),
                TargetId = report.TargetId,
                ReasonCode = report.ReasonCode,
                Detail = report.Detail,
                SourceType = report.SourceType.ToString(),
                CreatedAt = report.CreatedAt,
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

        private static string NormalizeReasonCode(string? reasonCode)
        {
            var normalizedReasonCode = (reasonCode ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedReasonCode.Length > MaxReasonCodeLength)
            {
                throw new BadRequestException("Reason code is too long.");
            }

            return normalizedReasonCode;
        }

        private static string? NormalizeDetail(string? detail)
        {
            var normalizedDetail = (detail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedDetail))
            {
                return null;
            }

            if (normalizedDetail.Length > MaxDetailLength)
            {
                throw new BadRequestException("Detail is too long.");
            }

            return normalizedDetail;
        }
    }
}
