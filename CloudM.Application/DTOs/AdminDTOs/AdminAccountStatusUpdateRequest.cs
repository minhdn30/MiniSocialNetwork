using CloudM.Domain.Enums;

namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminAccountStatusUpdateRequest
    {
        public AccountStatusEnum Status { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
