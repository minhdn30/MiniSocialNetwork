using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.EmailVerifications
{
    public class EmailVerificationRepository : IEmailVerificationRepository
    {
        private readonly AppDbContext _context;
        public EmailVerificationRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<EmailVerification?> GetByEmailAsync(string email)
        {
            return await _context.EmailVerifications
                .FirstOrDefaultAsync(e => e.Email == email);
        }

        public async Task AddEmailVerificationAsync(EmailVerification emailVerification)
        {
            await _context.EmailVerifications.AddAsync(emailVerification);
            await  _context.SaveChangesAsync();
        }
        public async Task UpdateEmailVerificationAsync(EmailVerification emailVerification)
        {
            _context.EmailVerifications.Update(emailVerification);
            await _context.SaveChangesAsync();
        }
        public async Task<bool> IsEmailExist(string email)
        {
            return await _context.EmailVerifications.AnyAsync(e => e.Email == email);
        }
        public async Task<bool> VerifyCodeAsync(string email, string code)
        {
            var latestVerification = await _context.EmailVerifications
                .Where(e => e.Email == email)
                .OrderByDescending(e => e.ExpiredAt) 
                .FirstOrDefaultAsync();

            if (latestVerification == null || latestVerification.ExpiredAt <= DateTime.UtcNow)
                return false;

            return latestVerification.Code == code;
        }

        public async Task DeleteEmailVerificationAsync(string email)
        {
            var verification = await _context.EmailVerifications
                .FirstOrDefaultAsync(e => e.Email == email);
            if (verification != null)
            {
                _context.EmailVerifications.Remove(verification);
                await _context.SaveChangesAsync();
            }
        }
    }
}
