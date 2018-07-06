using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Helperx.DomainClasses.Entities.Common;
using Helperx.DomainClasses.Entities.Common.ComplexTypes;
using Helperx.DomainClasses.Entities.Common.Enums;
using Helperx.DomainClasses.Entities.SocialNetworks.Contents;
using Helperx.DomainClasses.Entities.Users;

namespace Helperx.DomainClasses.Entities.SocialNetworks
{
    public class TelegramMember : IBaseEntity
    {
        public TelegramMember()
        {
         //   PortAdmins=new HashSet<PortAdmin>();
         //   TelegramBotMessages=new List<TelegramBotMessage>();
            TelegramMemberGuid=Guid.NewGuid();
          //  TelegramMemberBotSettings=new List<TelegramMemberBotSetting>();
            TelegramUser=new TelegramUser();
            Gender = true;
        }

        public int TelegramMemberId { get; set; }
        public Guid TelegramMemberGuid { get; set; }
        public TelegramUser TelegramUser { get; set; }
        public string TmentUsername { get; set; }

       
        /// <summary>
        /// نوع عنوان تیمنتی کاربر
        /// کاربر باید یکی را انتخاب کنه:
        ///     -   TmentUserName : 0
        ///     -   TmentNicName  : 1
        ///     -   TelegramUserName: 2
        ///     -   TelegramFirstNameLastName: 3
        /// </summary>
        public int TmentTitleType { get; set; }

        [NotMapped]
        public HxTmentTitleTypes TmentTitleTypeEnum
        {
            get { return TmentTitleType<0 ? 0 : (HxTmentTitleTypes)TmentTitleType; }
            set { TmentTitleType = (int) value; }
        }

        /// <summary>
        /// نام مستعار کاربر در سیستم تیمنت
        /// </summary>
        public string TmentNicName { get; set; } // nvarchar(30)    Checked

        public string PhotoUrl { get; set; }
        public bool Gender { get; set; }
        /// <summary>
        /// ورژن مسیر تصویر
        /// این ورژن در نقش چیزی شبیه یک کوئری استرینگ یا بخشی از یوآرال که میتواند سبب خوانده شدن نسخه جدید تصویر
        /// بجای نسخه کش‌شده شود میتواند به کار گرفته شود
        /// در مورد فایلهای آپلود شده روی کلاودینری هم این قابلیت به شکل خاص توضیح داده شده در
        /// داکیومنت کلاودینری میتونه در یو‌آر‌ال جاسازی بشه
        /// https://cloudinary.com/documentation/upload_images#image_versions
        /// کلادینری خودش این ورژن را هنگام آپلود شدن تصویر روی سرورش به ما برمیگرداند و ما هم آن را اینجا ذخیره میکنیم
        /// </summary>
        public string PhotoVersion { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
        public byte[] RowVersion { get; set; }


        //_____________________________ForeignKey Properties____________________________________
        public long? UserId { get; set; }

        //_____________________________Navigation Properties____________________________________
        /// <summary>
        /// هر کاربر تلگرامی ما که در سایت ثبت نام موفق داشته باشد باید این فیلد ویرچوال اش در این انتیتی به انتیتی کاربری اش در هلپریکس اشاره کند
        /// این اتصال در زمان ثبت نام بایست شکل گرفته باشد
        /// </summary>
        public virtual AppUser User { get; set; }
        public virtual ICollection<PortAdmin> PortAdmins { get; set; }
        public virtual IList<TelegramBotMessage> TelegramBotMessages { get; set; }
        public virtual IList<TelegramMemberBotSetting> TelegramMemberBotSettings { get; set; }
        public virtual IList<TelegramComment> TelegramComments { get; set; }

        /// <summary>
        ///  پیام‌های تلگرامی که از طریق ربات نظرات توسط این کاربر به کانالی ارسال شده است
        /// </summary>
        public virtual IList<TelegramPost> TelegramPosts { get; set; }

        public virtual ICollection<TelegramCommentLike> TelegramCommentLikes { get; set; }

        /// <summary>
        /// پورتهایی مه این کاربر تلگرامی سازنده آنهاست
        /// </summary>
        public virtual ICollection<TelegramPort> MyTelegramPorts { get; set; }

        
    }
}
