using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Services.Email
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
    }
}
