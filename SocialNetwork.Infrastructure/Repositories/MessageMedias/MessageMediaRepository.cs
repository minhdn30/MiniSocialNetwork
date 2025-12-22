using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.MessageMedias
{
    public class MessageMediaRepository : IMessageMediaRepository
    {
        private readonly AppDbContext _context;
        public MessageMediaRepository(AppDbContext context)
        {
            _context = context;
        }
    }
}
