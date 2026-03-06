using System.ComponentModel.DataAnnotations;

namespace CloudM.Application.DTOs.AuthDTOs
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]
        public string Email { get; set; } = string.Empty;
    }
}
