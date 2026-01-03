using MarketService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PortfolioService.Infrastructure.Data;

public class MarketConfig : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.MarketPubKey).IsRequired().HasMaxLength(64);
        b.Property(x => x.VaultPubKey).IsRequired().HasMaxLength(64);
        b.Property(x => x.VaultAuthorityPubKey).IsRequired().HasMaxLength(64);
        b.Property(x => x.CollateralMint).IsRequired().HasMaxLength(64);
        b.Property(x => x.ProgramId).IsRequired().HasMaxLength(64);
        b.Property(x => x.AuthorityPubKey).IsRequired().HasMaxLength(64);

        b.Property(x => x.Question).IsRequired().HasMaxLength(512);
        b.Property(x => x.CreatedTxSignature).IsRequired().HasMaxLength(128);
        
        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<byte>()        
            .IsRequired(false);

        b.Property(x => x.WinningOutcomeIndex)
            .HasColumnName("winning_outcome_index")
            .IsRequired(false);

        b.Property(x => x.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc")
            .IsRequired(false);

        b.Property(x => x.SettledAtUtc)
            .HasColumnName("settled_at_utc")
            .IsRequired(false);

        // Uniques
        b.HasIndex(x => x.MarketPubKey).IsUnique();
        b.HasIndex(x => new { x.AuthorityPubKey, x.MarketSeedId }).IsUnique(); // prevents accidental duplicates
    }
    
    public class MarketActionConfig : IEntityTypeConfiguration<MarketAction>
    {
        public void Configure(EntityTypeBuilder<MarketAction> b)
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestJson).IsRequired().HasColumnType("jsonb");
            b.Property(x => x.MarketId).IsRequired(false);
        
            b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired(false);

            b.Property(x => x.TxSignature).HasMaxLength(128);
            b.Property(x => x.ErrorCode).HasMaxLength(64);
            b.Property(x => x.RpcErrorText)
                .HasColumnType("text"); // long logs are common

            b.Property(x => x.AttemptCount)
                .HasDefaultValue(0);
            b.Property(x => x.CreatedAtUtc);

            b.Property(x => x.UpdatedAtUtc);
            b.Property(x => x.ConfirmedAtUtc);

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
        
            //Useful lookups
            b.HasIndex(x => new { x.State, x.CreatedAtUtc });
            b.HasIndex(x => new { x.ErrorCode, x.CreatedAtUtc });
            b.HasIndex(x => new { x.AttemptCount, x.CreatedAtUtc });
            b.HasIndex(x => new { x.MarketId, x.ActionType, x.State });

            b.HasOne(x => x.Market)
                .WithMany(m => m.Actions)
                .HasForeignKey(x => x.MarketId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
    
    public class UserMarketPositionConfig : IEntityTypeConfiguration<UserMarketPosition>
    {
        public void Configure(EntityTypeBuilder<UserMarketPosition> b)
        {
            b.HasKey(x => x.Id);
        
            b.Property(c => c.OwnerPubkey).IsRequired().HasMaxLength(64);
            b.Property(x => x.PositionPubKey).IsRequired().HasMaxLength(64);

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.Property(x => x.YesShares).HasColumnType("numeric(20,0)");
            b.Property(x => x.NoShares).HasColumnType("numeric(20,0)");
            b.Property(x => x.LastSyncedSlot).HasColumnType("numeric(20,0)");

            b.HasIndex(x => new { x.UserId, x.MarketId }).IsUnique();
            b.HasIndex(x => x.PositionPubKey).IsUnique();

            b.HasOne(x => x.Market)
                .WithMany()
                .HasForeignKey(x => x.MarketId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}