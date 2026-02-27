using System.ComponentModel.DataAnnotations;

namespace CloudM.Application.DTOs.AuthDTOs
{
    public class CompleteExternalProfileRequest
    {
        [Required(ErrorMessage = "Provider is required.")]
        public string Provider { get; set; } = string.Empty;

        [Required(ErrorMessage = "Credential is required.")]
        public string Credential { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = string.Empty;
    }
}
