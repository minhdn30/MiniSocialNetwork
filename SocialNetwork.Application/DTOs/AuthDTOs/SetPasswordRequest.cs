using System.ComponentModel.DataAnnotations;

namespace SocialNetwork.Application.DTOs.AuthDTOs
{
    public class SetPasswordRequest
    {
        [Required(ErrorMessage = "New password is required.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
