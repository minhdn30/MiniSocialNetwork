using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Exceptions;
using SocialNetwork.Application.Interfaces;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public AuthService(IAccountRepository accountRepository, IMapper mapper)
        {
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<RegisterResponse> RegisterAsync(RegisterDTO registerRequest)
        {
            var usernameExists = await _accountRepository.IsUsernameExist(registerRequest.Username);
            if(usernameExists)
            {
                throw new BadRequestException("Username already exists.");
            }
            var emailExists = await _accountRepository.IsEmailExist(registerRequest.Email);
            if(emailExists)
            {
                throw new BadRequestException("Email already exists.");
            }
            var account = _mapper.Map<Account>(registerRequest);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password);
            account.RoleId = (int)RoleEnum.User;
            account.IsEmailVerified = false;
            await _accountRepository.AddAccount(account);

            var accountMapped = _mapper.Map<RegisterResponse>(account);
            return accountMapped;
        }
    }
}
