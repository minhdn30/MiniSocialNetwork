using System.ComponentModel.DataAnnotations;

namespace CloudM.Application.DTOs.AuthDTOs
{
    public class ExternalLoginRequest
    {
        [Required(ErrorMessage = "Provider is required.")]
        public string Provider { get; set; } = string.Empty;

        [Required(ErrorMessage = "Credential is required.")]
        public string Credential { get; set; } = string.Empty;
    }
}
