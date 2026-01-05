using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Data;

public class AuthDbConfig : IEntityTypeConfiguration<WalletLoginChallenge>
{
    public void Configure(EntityTypeBuilder<WalletLoginChallenge> builder)
    {
        builder.ToTable("WalletLoginChallenges",  "auth");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.WalletPubkey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Nonce).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2048);

        builder.HasIndex(x => new {x.WalletPubkey, x.Nonce}).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}