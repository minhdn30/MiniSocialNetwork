using System.ComponentModel.DataAnnotations;

namespace CloudM.Application.DTOs.AuthDTOs
{
    public class ForgotPasswordVerifyRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }
}
