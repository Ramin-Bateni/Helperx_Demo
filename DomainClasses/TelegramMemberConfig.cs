using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration;
using Helperx.DomainClasses.Configurations.Common;
using Helperx.DomainClasses.Entities.SocialNetworks;

namespace Helperx.DomainClasses.Configurations.SocialNetworks
{
    public class TelegramMemberConfig : EntityTypeConfiguration<TelegramMember>
    {
        
        //private const string IX_Link_ADMIN_ID = "IX_LinkAdminId";

        public TelegramMemberConfig()
        {
            ToTable(DbConsts.TableNames.TELEGRAM_MEMBER);
            HasKey(a => a.TelegramMemberId);

            Property(p => p.TelegramMemberGuid)
                .IsRequired()
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute(DbConsts.Indexes.TELEGRAM_MEMBER_TELEGRAM_MEMBER_GUID) { IsUnique = true }));

            // برای این فیلد باید ایندکس یونیک نالیبل تعریف شود 
            Property(p => p.TmentUsername)
                .IsOptional()
                .HasMaxLength(32);

            Property(p => p.TmentNicName)
                .IsOptional()
                .HasMaxLength(30);

            Property(p => p.TmentTitleType).IsRequired();

            Property(p => p.Gender).IsRequired();
            Property(p => p.PhotoUrl).IsOptional().HasMaxLength(1024);
            Property(p => p.PhotoVersion).IsOptional().HasMaxLength(20);

            
            Property(p => p.TelegramUser.UserId)
                .IsRequired()
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute(DbConsts.Indexes.TELEGRAM_MEMBER_USER_ID) { IsUnique = true }));

            Property(a => a.TelegramUser.FirstName)
                .HasMaxLength(50)
                .IsOptional();

            Property(a => a.TelegramUser.LastName)
                .HasMaxLength(50)
                .IsOptional();

            Property(a => a.TelegramUser.Username)
                .HasMaxLength(100)
                .IsOptional()
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute(DbConsts.Indexes.TELEGRAM_MEMBER_USERNAME) { IsUnique = false }));

            Property(p => p.RowVersion).IsRowVersion();

            HasOptional(c => c.User)
                .WithMany(x => x.TelegramMembers)
                .HasForeignKey(x => x.UserId)
                .WillCascadeOnDelete(false);

        }
    }
}