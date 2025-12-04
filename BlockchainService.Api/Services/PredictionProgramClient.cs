using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Solnet.KeyStore;
using Solnet.Programs.Utilities;

namespace BlockchainService.Api.Services
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

    public class PredictionProgramClient
    {
        private readonly IRpcClient _rpc;
        private readonly Account _authority;
        private readonly PublicKey _programId;

        public static Account LoadAuthorityAccount(string path)
        {
            var json = File.ReadAllText(path);
            var keystore = new SolanaKeyStoreService();

            //for typical id.json with no passpharse
            var wallet = keystore.RestoreKeystore(json);

            //Split 64-bytes Solana key pair
            // var privateKey = keyBytes.Take(32).ToArray();
            //var publicKey = keyBytes.Skip(32).ToArray();

            //Use Solnet's Wallet.account here to read the 64-byte secret key.
            Account account = wallet.Account;
            Console.WriteLine($"[Authority] Authority pubkey = '{account.PublicKey.Key}'");
            return account;
        }

        public PredictionProgramClient(IOptions<SolanaOptions> options)
        {
            var cfg = options.Value;

            Console.WriteLine($"[PredictionProgramClient] RpcUrl = '{cfg.RpcUrl}'");
            Console.WriteLine($"[PredictionProgramClient] ProgramId = '{cfg.ProgramId}'");
            Console.WriteLine($"[PredictionProgramClient] AuthorityKeyPairPath = '{cfg.AuthorityKeypairPath}'");

            _rpc = ClientFactory.GetClient(cfg.RpcUrl);
            _programId = new PublicKey(cfg.ProgramId);

            // 🔹 For now: generate a NEW authority keypair in .NET
            _authority = LoadAuthorityAccount(cfg.AuthorityKeypairPath);
            Console.WriteLine($"[PredictionProgramClient] Authority pubkey = '{_authority.PublicKey.Key}'");
            Console.WriteLine("⚠ Remember to airdrop some devnet SOL to this authority!");
        }

        public async Task<MarketResult> CreateMarketAsync(
            string question,
            string[] outcomes,
            DateTime endTimeUtc,
            string collateralMint,
            string vaultTokenAccount)
        {
            var collateralMintPk = new PublicKey(collateralMint);
            var vaultPk = new PublicKey(vaultTokenAccount);

            var marketAccount = new Account();
            var marketPubkey = marketAccount.PublicKey;

            var ixData = BuildCreateMarketInstructionData(
                question,
                outcomes,
                collateralMintPk,
                endTimeUtc
            );

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
            {
                throw new Exception("Failed to get recent blockhash: " +
                                    blockhashResult.Reason);
            }

            var latest = blockhashResult.Result.Value;
            var recentBlockhash = latest.Blockhash;

            Console.WriteLine($"[CreateMarketAsync] Using blockhash: {recentBlockhash}");

            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPubkey, true),
                AccountMeta.Writable(vaultPk, false),
                AccountMeta.Writable(_authority.PublicKey, true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority)
                .AddInstruction(ix)
                .Build(new[] { _authority, marketAccount });

            var simResult = await _rpc.SimulateTransactionAsync(tx);

            if (!simResult.WasSuccessful || simResult.Result?.Value == null) {
                Console.WriteLine($"[Simulate] failed to simulate transaction: {simResult.Reason}");
                throw new Exception($"Simualtion Failed: {simResult.Reason}");
            }

            if (simResult.Result.Value.Logs != null) {
                Console.WriteLine("=== Simulation Logs ===");
                foreach (var log in simResult.Result.Value.Logs) 
                { 
                  Console.WriteLine(log);  
                }
                Console.WriteLine("=== End logs");
            }

            var txSigResult = await _rpc.SendTransactionAsync(tx);
            if (!txSigResult.WasSuccessful)
            {
                throw new Exception("Failed to send create_market tx: " +
                                    txSigResult.Reason);
            }

            return new MarketResult
            {
                MarketAction = "Create Action",
                MarketPubkey = marketPubkey.Key,
                TransactionSignature = txSigResult.Result
            };
        }

        public async Task<MarketResult> ResolveMarketAsync(
        string marketPubkey,
        byte outcomeIndex)
        {
            var marketPk = new PublicKey(marketPubkey);

            // ----- Build instruction data -----
            // discriminator = sha256("global:resolve_market")[0..8]
            var ixData = BuildResolveMarketInstructionData(outcomeIndex);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception("Failed to get latest blockhash: " + blockhashResult.Reason);

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                // market (writable, not signer)
                AccountMeta.Writable(marketPk, false),

                // authority (signer, writable)
                AccountMeta.Writable(_authority.PublicKey, true),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority)
                .AddInstruction(ix)
                .Build(new[] { _authority });

            // Optional: simulate for logs
            var simResult = await _rpc.SimulateTransactionAsync(tx);
            if (!simResult.WasSuccessful || simResult.Result?.Value == null)
                throw new Exception("Simulation failed: " + simResult.Reason);

            if (simResult.Result.Value.Logs != null)
            {
                Console.WriteLine("=== ResolveMarket simulation logs ===");
                foreach (var log in simResult.Result.Value.Logs)
                    Console.WriteLine(log);
                Console.WriteLine("=== End logs ===");
            }

            var txSigResult = await _rpc.SendTransactionAsync(tx);
            if (!txSigResult.WasSuccessful)
                throw new Exception("Failed to send resolve_market tx: " + txSigResult.Reason);

            return new MarketResult
            {
                MarketAction = "Resolve Market",
                MarketPubkey = marketPk.Key,
                TransactionSignature = txSigResult.Result
            };
        }

        public async Task<PlaceBetResult> PlaceBetAsync(
            string marketPubkey,
            string bettorTokenAccount,
            string vaultTokenAccount,
            ulong stakeAmount,
            byte outcomeIndex)
        {
            var marketPk = new PublicKey(marketPubkey);
            var bettorTokenpk = new PublicKey(bettorTokenAccount);
            var vaultTokenPk = new PublicKey(vaultTokenAccount);

            //derive position PDA
            //seeds = [b"position", market.key().as_ref(), user.key().as_ref()]
            byte[][] seeds =
            {
                Encoding.UTF8.GetBytes("position"),
                marketPk.KeyBytes,
                _authority.PublicKey.KeyBytes //user = authority for now
            };

            if (!PublicKey.TryFindProgramAddress(seeds, _programId, out var positionPk, out var bumpSeed))
                throw new Exception($"Failed to derive position PDA.");

            //build ix data: discriminator + stake (u64) + outcome_index (u8)
            var ixData = BuildPlaceBetInstructionDate(stakeAmount, outcomeIndex);

            //fresh blockhash
            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            //accounts: 
            var accounts = new List<AccountMeta>
            {
                //0. market
                AccountMeta.Writable(marketPk, false),
                //1. position PDA
                AccountMeta.Writable(positionPk, false),
                //2. bettor (signer, mut)
                AccountMeta.Writable(_authority.PublicKey, true),
                //3. bettor_collateral_ata (mut)
                AccountMeta.Writable(bettorTokenpk, false),
                //4. vault (mut)
                AccountMeta.Writable(vaultTokenPk, false),
                //5. token_program
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                //6. system_program
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority)
                .AddInstruction(ix)
                .Build(new[] { _authority });

            // Optional: simulate for logs
            var simResult = await _rpc.SimulateTransactionAsync(tx);
            if (!simResult.WasSuccessful || simResult.Result?.Value == null)
                throw new Exception("Simulation failed: " + simResult.Reason);

            if (simResult.Result.Value.Logs != null)
            {
                Console.WriteLine("=== PlaceBet simulation logs ===");
                foreach (var log in simResult.Result.Value.Logs)
                    Console.WriteLine(log);
                Console.WriteLine("=== End logs ===");
            }

            var txSigResult = await _rpc.SendTransactionAsync(tx);
            if (!txSigResult.WasSuccessful)
                throw new Exception("Failed to send place_bet tx: " + txSigResult.Reason);

            Console.WriteLine($"[PlaceBet] Tx signature: {txSigResult.Result}");

            return new PlaceBetResult
            {
                MarketPubkey = marketPk.Key,
                BettorTokenAccount = bettorTokenpk.Key,
                StakeAmount = stakeAmount,
                OutcomeIndex = outcomeIndex,
                TransactionSignature = txSigResult.Result
            };
        }

        private static byte[] BuildCreateMarketInstructionData(
            string question,
            string[] outcomes,
            PublicKey collateralMint,
            DateTime endTimeUtc)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            var discriminator = GetAnchorDiscriminator("global", "create_market");
            bw.Write(discriminator);

            WriteBorshString(bw, question);

            bw.Write((int)outcomes.Length);
            foreach (var outcome in outcomes)
            {
                WriteBorshString(bw, outcome);
            }

            bw.Write(collateralMint.KeyBytes);

            long unixSeconds = new DateTimeOffset(endTimeUtc.ToUniversalTime())
                .ToUnixTimeSeconds();
            bw.Write(unixSeconds);

            bw.Flush();
            return ms.ToArray();
        }

        private static byte[] GetAnchorDiscriminator(string ns, string name)
        {
            var input = $"{ns}:{name}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return hash.Take(8).ToArray();
        }

        private static void WriteBorshString(BinaryWriter bw, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            bw.Write((int)bytes.Length);
            bw.Write(bytes);
        }

        private static byte[] BuildResolveMarketInstructionData(byte outcomeIndex)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // discriminator for resolve_market
            var discriminator = GetAnchorDiscriminator("global", "resolve_market");
            bw.Write(discriminator);

            // args: u8 outcome_index
            bw.Write(outcomeIndex);

            bw.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildPlaceBetInstructionDate(ulong stakeAmount, byte outcomeIndex)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // 1) discriminator: "global:place_bet"
            var discriminator = GetAnchorDiscriminator("global", "place_bet");
            bw.Write(discriminator);

            //Important must match Rust arg order:
            // 2) outcome_index: u8 (first arg)
            bw.Write(outcomeIndex);

            //3) amount: u64 (second arg)
            bw.Write(stakeAmount);

            bw.Flush();
            return ms.ToArray();
        }
    }
}
