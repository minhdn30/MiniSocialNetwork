using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.MessageMedias
{
    public interface IMessageMediaRepository
    {
        Task AddMessageMediasAsync(List<MessageMedia> medias);
    }
}
