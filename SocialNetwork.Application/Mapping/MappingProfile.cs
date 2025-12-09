using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            //Account mappings
            CreateMap<Account, RegisterResponse>();
            CreateMap<RegisterDTO, Account>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username.ToLower()));
            CreateMap<Account, AccountOverviewResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName));
            CreateMap<Account, AccountDetailResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName));
            CreateMap<AccountCreateRequest, Account>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());
            CreateMap<AccountUpdateRequest, Account>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<ProfileUpdateRequest, Account>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}
