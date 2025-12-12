using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.PostReactServices
{
    public class PostReactService : IPostReactService
    {
        private readonly IPostReactRepository _postReactRepository;
        private readonly IMapper _mapper;
        public PostReactService(IPostReactRepository postReactRepository, IMapper mapper)
        {
            _postReactRepository = postReactRepository;
            _mapper = mapper;
        }
        public async Task<ReactToggleResponse> ToggleReactOnPost(Guid postId, Guid accountId)
        {
            var existingReact = await _postReactRepository.GetUserReactOnPostAsync(postId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _postReactRepository.RemovePostReact(existingReact);
                isReactedByCurrentUser = false;
            }
            else
            {
                var newReact = new PostReact
                {
                    PostId = postId,
                    AccountId = accountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = DateTime.UtcNow
                };
                await _postReactRepository.AddPostReact(newReact);
                isReactedByCurrentUser = true;
            }
            var reactCount = await _postReactRepository.GetReactCountByPostId(postId);
            return new ReactToggleResponse
            {
                ReactCount = reactCount,
                IsReactedByCurrentUser = isReactedByCurrentUser
            };
        }
        public async Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnPostPaged(Guid postId, int page, int pageSize)
        {
            var (reacts, totalItems) = await _postReactRepository.GetAccountsReactOnPostPaged(postId, page, pageSize);
            return new PagedResponse<AccountReactListModel>
            {
                Items = reacts,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }
    }
}
