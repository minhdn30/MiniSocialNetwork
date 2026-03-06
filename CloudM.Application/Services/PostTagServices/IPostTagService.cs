using System;
using System.Threading.Tasks;
using CloudM.Application.DTOs.PostDTOs;

namespace CloudM.Application.Services.PostTagServices
{
    public interface IPostTagService
    {
        Task<PostTagSummaryResponse> UntagMeFromPost(Guid postId, Guid currentId);
    }
}
