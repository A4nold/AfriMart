using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketService.Domain.Entities
{
    public class Market
    {
        public Guid Id { get; set; }

        // Your deterministic seed used for PDA derivation (u64)
        public ulong MarketSeedId { get; set; }

        // On-chain pointers
        public string ProgramId { get; set; } = default!;
        public string AuthorityPubKey { get; set; } = default!;
        public string MarketPubKey { get; set; } = default!;           // market_v2 PDA (unique)
        public string VaultPubKey { get; set; } = default!;            // vault_v2 PDA
        public string VaultAuthorityPubKey { get; set; } = default!;   // vault_auth_v2 PDA
        public string CollateralMint { get; set; } = default!;         // mint pubkey

        // Product/UX
        public string Question { get; set; } = default!;
        public DateTime EndTimeUtc { get; set; }

        public Guid CreatorUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        // Transaction pointers
        public string CreatedTxSignature { get; set; } = default!;

        // --- Cached fields (optional, clearly “cached”) ---
        public MarketStatus? Status { get; set; }
        public byte? WinningOutcomeIndex { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public DateTime? SettledAtUtc { get; set; }

        public ulong? LastIndexedSlot { get; set; }
        public DateTime? LastSyncedAtUtc { get; set; }

        // Navigation
        public ICollection<MarketOutcome> Outcomes { get; set; } = new List<MarketOutcome>();
        public MarketResolution? Resolution { get; set; }
        public ICollection<MarketAction> Actions { get; set; } = new List<MarketAction>();
    }

}
