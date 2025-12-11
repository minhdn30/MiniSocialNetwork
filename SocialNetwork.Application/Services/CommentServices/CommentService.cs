using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Posts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.CommentServices
{
    public class CommentService : ICommentService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public CommentService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IMapper mapper)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request)
        {
            if(!await _postRepository.IsPostExist(postId))
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new BadRequestException($"Account with ID {accountId} not found.");
            }
            var comment = _mapper.Map<Comment>(request);
            comment.PostId = postId;
            comment.AccountId = accountId;
            await _commentRepository.AddComment(comment);
            var result = _mapper.Map<CommentResponse>(comment);
            result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
            return result;
        }
    }
}
