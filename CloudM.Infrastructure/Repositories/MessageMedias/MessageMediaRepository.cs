using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.MessageMedias
{
    public class MessageMediaRepository : IMessageMediaRepository
    {
        private readonly AppDbContext _context;
        public MessageMediaRepository(AppDbContext context)
        {
            _context = context;
        }
        public Task AddMessageMediasAsync(List<MessageMedia> medias)
        {
            return _context.MessageMedias.AddRangeAsync(medias);
        }

        public Task<MessageMedia?> GetByIdWithMessageAsync(Guid messageMediaId)
        {
            return _context.MessageMedias
                .Include(mm => mm.Message)
                .FirstOrDefaultAsync(mm => mm.MessageMediaId == messageMediaId);
        }
    }
}
