using System;
using AutoMapper;
using Helperx.AutoMapperProfiles.Extentions;
using Helperx.Common;
using Helperx.Common.Helpers;
using Helperx.DomainClasses.Entities.Common.ComplexTypes;
using Helperx.DomainClasses.Entities.SocialNetworks;
using Helperx.Utility;
using Helperx.ViewModel.Areas.ControlPanel.Hlink;
using Helperx.ViewModel.Telegram;
using Telegram.Bot.Types;

namespace Helperx.AutoMapperProfiles
{
    public class TelegramPortProfile : Profile
    {
        protected override void Configure()
        {
            CreateMap<AddTelegramPortViewModel, TelegramPort>()
                //----------------------------------------------------------------------------------------------
                //--------------------------شروط لازم برای مپ اطلاعات بخصوص زمان آپدیت-------------------------
                //------------چراکه نمیخوایهم اطلاعات نال و صفر و نامعتبر روی اطلاعات معتبر ریخته شود--------
                //----------------------------------------------------------------------------------------------
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username.Trim().RemoveFirstAtsign()))
                .ForMember(dest => dest.Username, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Username)))
                .ForMember(dest => dest.CreatorTelegramMemberId, opt => opt.MapFrom(src => src.CreatorTelegramMemberId))
                .ForMember(dest => dest.Identifier, opt => opt.MapFrom(src => src.Identifier))
                .ForMember(dest => dest.Identifier, opt => opt.Condition(src => src.Identifier != 0))
                .ForMember(dest => dest.AccessHash, opt => opt.Condition(src => src.AccessHash != 0 && src.AccessHash != null))
                .ForMember(dest => dest.TitleFa, opt => opt.Condition(src => !string.IsNullOrEmpty(src.TitleFa)))
                .ForMember(dest => dest.TitleEn, opt => opt.Condition(src => !string.IsNullOrEmpty(src.TitleEn)))
                .ForMember(dest => dest.AdminsInfo, opt => opt.Condition(src => !string.IsNullOrEmpty(src.AdminsInfo)))
                .ForMember(dest => dest.LanguageId, opt => opt.MapFrom(src => src.LanguageId > 0 ? src.LanguageId : (int) Consts.SOCIAL_PORT_DEFAULT_LANGUAGE))
                .ForMember(dest => dest.FollowerCount, opt => opt.Condition(src => src.FollowerCount > 0 && src.FollowerCount != null))
                .ForMember(dest => dest.PostCount, opt => opt.Condition(src => src.PostCount > 0 && src.PostCount != null))
                .ForMember(dest => dest.DescriptionFa, opt => opt.Condition(src => !string.IsNullOrEmpty(src.DescriptionFa)))
                .ForMember(dest => dest.DescriptionEn, opt => opt.Condition(src => !string.IsNullOrEmpty(src.DescriptionEn)))
                .ForMember(dest => dest.Website, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Website)))
                .ForMember(dest => dest.FollowerCountUpdateDt, opt => opt.Condition(src => src.FollowerCountUpdateDt != null))
                .ForMember(dest => dest.Owner, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Owner)))
                .ForMember(dest => dest.InviteLink, opt => opt.Condition(src => !string.IsNullOrEmpty(src.InviteLink)))
                .ForMember(dest => dest.ProfilePhotoUrl, opt => opt.Condition(src => !string.IsNullOrEmpty(src.ProfilePhotoUrl)))
                .ForMember(dest => dest.PortTypeId, opt => opt.MapFrom(src => src.PortTypeEnum.ToInt()))
                .ForMember(dest => dest.Port, opt => opt.MapFrom(src => src.Port))
                .ForMember(dest => dest.PortType, opt => opt.Ignore())
                .IgnoreAllNonExisting();

            //CreateMap<AddNormalTelegramPortViewModel, TelegramPort>()
            //    // افزودن اتساین به اول یوزرنیم کانال، اگر یوزرنیم خالی نبود و وقتی ترایمش هم میکردیم خالی نمیشد و اتساین دار هم نبود
            //    .ForMember(d => d.ChatUsername, m => m.MapFrom(x => x.ChatUsername.AddAtsignToUsernameIfHasNot()))
            //    .IgnoreAllNonExisting();
        }
        
        public override string ProfileName => GetType().Name;

        
    }

}
