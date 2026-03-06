using CloudM.Application.DTOs.PostDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PostSaveServices
{
    public interface IPostSaveService
    {
        Task<PostSaveStateResponse> SavePostAsync(Guid currentId, Guid postId);
        Task<PostSaveStateResponse> UnsavePostAsync(Guid currentId, Guid postId);
        Task<(List<PostPersonalListModel> Items, bool HasMore)> GetSavedPostsByCursorAsync(
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit);
    }
}
