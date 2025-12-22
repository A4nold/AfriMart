using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockchainService.Inrastructure.Helpers
{
    public class SolanaOptions
    {
        public string RpcUrl { get; set; } = default!;
        public string ProgramId { get; set; } = default!;
        public string AuthorityKeypairPath { get; set; } = default!;

        public bool SimulateBeforeSend { get; set; } = true;

        public string Commitment { get; set; } = "processed"; // processed/confirmed/finalized
        public bool SkipPreflight { get; set; } = false;
        public uint ComputeUnitLimit { get; set; } = 300_000;
        public ulong ComputeUnitPriceMicroLamports { get; set; } = 0; // 0 disables priority fees
    }

    public class MarketResult
    {
        public string MarketAction { get; set; } = default!;
        public string MarketPubkey { get; set; } = default!;
        public string TransactionSignature { get; set; } = default!;
    }

    public class BuyShareResult
    {
        public string MarketPubkey { get; set; } = default!;
        public string UserCollateralAta { get; set; } = default!;
        public ulong MaxCollateralIn { get; set; }
        public ulong MinSharesOut { get; set; }
        public byte OutcomeIndex { get; set; }
        public string TransactionSignature { get; set; } = default!;
    }

    public class SellShareResult
    {
        public string MarketPubkey { get; set; } = default!;
        public string UserCollateralAta { get; set; } = default!;
        public ulong SharesIn { get; set; }
        public ulong MinCollateralOut { get; set; }
        public byte OutcomeIndex { get; set; }
        public string TransactionSignature { get; set; } = default!;
    }
}
