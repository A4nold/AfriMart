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
        // We won’t use AuthorityKeypairPath for now
        public string AuthorityKeypairPath { get; set; } = default!;
    }

    public class MarketResult
    {
        public string MarketAction { get; set; } = default!;
        public string MarketPubkey { get; set; } = default!;
        public string TransactionSignature { get; set; } = default!;
    }

    public class PlaceBetResult
    {
        public string MarketPubkey { get; set; } = default!;
        public string BettorTokenAccount { get; set; } = default!;
        public ulong StakeAmount { get; set; }
        public byte OutcomeIndex { get; set; }
        public string TransactionSignature { get; set; } = default!;
    }
}
