using BlockchainService.Inrastructure.Helpers;
using Microsoft.Extensions.Options;
using Solnet.KeyStore;
using Solnet.Programs;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockchainService.Api.Services
{
    public class PredictionProgramClient
    {
        private readonly IRpcClient _rpc;
        private readonly Account _authority;
        private readonly PublicKey _programId;
        private readonly SolanaOptions _cfg;

        private static Account LoadAuthorityAccount(string path)
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

            _cfg = cfg;
            _rpc = ClientFactory.GetClient(cfg.RpcUrl);
            _programId = new PublicKey(cfg.ProgramId);

            // 🔹 For now: generate a NEW authority keypair in .NET
            _authority = LoadAuthorityAccount(cfg.AuthorityKeypairPath);
            Console.WriteLine($"[PredictionProgramClient] Authority pubkey = '{_authority.PublicKey.Key}'");
            Console.WriteLine("⚠ Remember to airdrop some devnet SOL to this authority!");
        }

        private Commitment GetCommitment(string commitment)
            => commitment.ToLowerInvariant() switch
            {
                "processed" => Commitment.Processed,
                "finalized" => Commitment.Finalized,
                _ => Commitment.Confirmed
            };
        
        private async Task<string> SendAndConfirmAsync(byte[] tx, Commitment commitment, bool skipPreflight, CancellationToken ct = default)
        {
            var send = await _rpc.SendTransactionAsync(tx, skipPreflight: skipPreflight, commitment: commitment);
            if (!send.WasSuccessful)
                throw new Exception($"SendTransaction failed: {send.Reason}");
        
            var sig = send.Result;
            
            bool IsAcceptable(string? confirmationStatus)
            {
                return commitment switch
                {
                    Commitment.Processed => confirmationStatus is "processed" or "confirmed" or "finalized",
                    Commitment.Confirmed => confirmationStatus is "confirmed" or "finalized",
                    Commitment.Finalized => confirmationStatus is "finalized",
                    _ => confirmationStatus is "confirmed" or "finalized"
                };
            }
        
            // Poll status until confirmed/finalized
            for (var i = 0; i < 60; i++)
            {
                ct.ThrowIfCancellationRequested();
                var status = await _rpc.GetSignatureStatusesAsync(new List<string> { sig },
                    searchTransactionHistory: true);
                if (status.WasSuccessful && status.Result?.Value != null)
                {
                    var s = status.Result.Value.FirstOrDefault();
                    if (s != null)
                    {
                        if (s.Error != null)
                            throw new Exception($"Transaction failed on-chain: {JsonSerializer.Serialize(s.Error)}");
        
                        // If we asked for confirmed, treat confirmed/finalized as success
                        if (IsAcceptable(s.ConfirmationStatus))
                            return sig;
                    }
                }
        
                await Task.Delay(500, ct);
            }
        
            throw new Exception($"Transaction not confirmed in time. Signature: {sig}");
        }

        public async Task<MarketResult> CreateMarketAsync(
            ulong marketId,
            string question,
            DateTime endTimeUtc,
            ulong initialLiquidity,
            string collateralMint,
            string authorityCollateralAta,
            CancellationToken ct = default)
        {
            var collateralMintPk = new PublicKey(collateralMint);
            var authorityCollateralAtaPk = new PublicKey(authorityCollateralAta);
            
            var marketIdBytes = BitConverter.GetBytes(marketId); // little-endian on all normal runtimes
            if (!BitConverter.IsLittleEndian) Array.Reverse(marketIdBytes);
            
            if (initialLiquidity == 0)
                throw new ArgumentException("initialLiquidity must be > 0", nameof(initialLiquidity));

            // market PDA: ["market_v2", authority, market_id_le]
            byte[][] marketSeeds =
            {
                Encoding.UTF8.GetBytes("market_v2"),
                _authority.PublicKey.KeyBytes,
                marketIdBytes 
            };
            if (!PublicKey.TryFindProgramAddress(marketSeeds, _programId, out var marketPk, out _))
                throw new Exception("Failed to derive market PDA.");

            // vault PDA: ["vault_v2", market]
            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            // vault authority PDA: ["vault_auth_v2", market]
            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            var ixData = BuildCreateMarketInstructionData(
                marketId,
                question,
                endTimeUtc,
                initialLiquidity
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
                // 0 market (init PDA) - writable, NOT signer
                AccountMeta.Writable(marketPk, false),

                // 1 vault (init PDA token account) - writable, NOT signer
                AccountMeta.Writable(vaultPk, false),

                // 2 vault_authority (PDA) - read-only
                AccountMeta.ReadOnly(vaultAuthorityPk, false),

                // 3 collateral_mint
                AccountMeta.ReadOnly(collateralMintPk, false),

                // 4 authority (payer)
                AccountMeta.Writable(_authority.PublicKey, true),

                // 5 authority_collateral_ata
                AccountMeta.Writable(authorityCollateralAtaPk, false),

                // 6 token_program
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),

                // 7 system_program
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),

                // 8 rent
                AccountMeta.ReadOnly(SysVars.RentKey, false),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority.PublicKey);

            foreach (var ixBudget in BuildComputeBudgetIxs())
            {
                txBuilder.AddInstruction(ixBudget);
            }

            txBuilder.AddInstruction(ix);
            var tx = txBuilder.Build(new[] { _authority });
            
            if (_cfg.SimulateBeforeSend)
            {
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
            }
            
            
            var txSignature = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new MarketResult
            {
                MarketAction = "Create Market Action",
                MarketPubkey = marketPk.Key,
                TransactionSignature = txSignature
            };
        }

        public async Task<MarketResult> ResolveMarketAsync(
        string marketPubkey,
        byte outcomeIndex,
        CancellationToken ct = default(CancellationToken))
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

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority.PublicKey);

            foreach (var ixBudget in BuildComputeBudgetIxs())
            {
                txBuilder.AddInstruction(ixBudget);
            }

            txBuilder.AddInstruction(ix);
            var tx = txBuilder.Build(new[] { _authority });

            // Optional: simulate for logs
            if (_cfg.SimulateBeforeSend)
            {
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
            }
            
            var txSignature = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight);

            return new MarketResult
            {
                MarketAction = "Resolve Market",
                MarketPubkey = marketPk.Key,
                TransactionSignature = txSignature
            };
        }

        public async Task<BuyShareResult> BuySharesAsync(
            string marketPubkey,
            string userCollateralAta,
            ulong maxCollateralIn,
            ulong minSharesOut,
            byte outcomeIndex,
            CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);
            var userCollateralPk = new PublicKey(userCollateralAta);

            // vault PDA: ["vault_v2", market]
            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            // vault_authority PDA: ["vault_auth_v2", market]
            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            // position PDA: ["position_v2", market, user]
            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                _authority.PublicKey.KeyBytes // MVP user = authority
            };
            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            // ix data: discriminator("buy_shares") + outcome_index(u8) + max_in(u64) + min_out(u64)
            var ixData = BuildBuySharesInstructionData(outcomeIndex, maxCollateralIn, minSharesOut);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                // 0 market (mut)
                AccountMeta.Writable(marketPk, false),

                // 1 vault (mut)
                AccountMeta.Writable(vaultPk, false),

                // 2 vault_authority (read-only)
                AccountMeta.ReadOnly(vaultAuthorityPk, false),

                // 3 position (mut PDA)
                AccountMeta.Writable(positionPk, false),

                // 4 user (mut signer)
                AccountMeta.Writable(_authority.PublicKey, true),

                // 5 user_collateral_ata (mut)
                AccountMeta.Writable(userCollateralPk, false),

                // 6 token_program
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),

                // 7 system_program
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),

                // 8 rent
                AccountMeta.ReadOnly(SysVars.RentKey, false),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority.PublicKey);

            foreach (var ixBudget in BuildComputeBudgetIxs())
                txBuilder.AddInstruction(ixBudget);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

            if (_cfg.SimulateBeforeSend)
            {
                var sim = await _rpc.SimulateTransactionAsync(tx);
                if (!sim.WasSuccessful || sim.Result?.Value == null)
                    throw new Exception("Simulation failed: " + sim.Reason);

                if (sim.Result.Value.Logs != null)
                    foreach (var log in sim.Result.Value.Logs) Console.WriteLine(log);
            }

            var sig = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new BuyShareResult
            {
                MarketPubkey = marketPk.Key,
                UserCollateralAta = userCollateralPk.Key,
                MaxCollateralIn = maxCollateralIn,
                OutcomeIndex = outcomeIndex,
                TransactionSignature = sig
            };
        }
        
        public async Task<SellShareResult> SellSharesAsync(
            string marketPubkey,
            string userCollateralAta,
            ulong sharesIn,
            ulong minCollateralOut,
            byte outcomeIndex,
            CancellationToken ct = default)
        {
            if (outcomeIndex > 1) throw new ArgumentOutOfRangeException(nameof(outcomeIndex), "OutcomeIndex must be 0 or 1.");
            if (sharesIn == 0) throw new ArgumentException("SharesIn must be > 0", nameof(sharesIn));

            var marketPk = new PublicKey(marketPubkey);
            var userCollateralPk = new PublicKey(userCollateralAta);

            // vault PDA: ["vault_v2", market]
            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            // vault_authority PDA: ["vault_auth_v2", market]
            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            // position PDA: ["position_v2", market, user]
            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                _authority.PublicKey.KeyBytes // MVP: user == authority
            };
            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            // ix data: discriminator("sell_shares") + outcome_index(u8) + shares_in(u64) + min_collateral_out(u64)
            var ixData = BuildSellSharesInstructionData(outcomeIndex, sharesIn, minCollateralOut);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            // Must match Anchor SellShares order exactly
            var accounts = new List<AccountMeta>
            {
                // 0 market (mut)
                AccountMeta.Writable(marketPk, false),

                // 1 vault (mut)
                AccountMeta.Writable(vaultPk, false),

                // 2 vault_authority (read-only)
                AccountMeta.ReadOnly(vaultAuthorityPk, false),

                // 3 position (mut)
                AccountMeta.Writable(positionPk, false),

                // 4 user (mut, signer)
                AccountMeta.Writable(_authority.PublicKey, true),

                // 5 user_collateral_ata (mut)
                AccountMeta.Writable(userCollateralPk, false),

                // 6 token_program
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority.PublicKey);

            foreach (var ixBudget in BuildComputeBudgetIxs())
                txBuilder.AddInstruction(ixBudget);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

            if (_cfg.SimulateBeforeSend)
            {
                var sim = await _rpc.SimulateTransactionAsync(tx);
                if (!sim.WasSuccessful || sim.Result?.Value == null)
                    throw new Exception("Simulation failed: " + sim.Reason);

                if (sim.Result.Value.Logs != null)
                {
                    Console.WriteLine("=== sell_shares simulation logs ===");
                    foreach (var log in sim.Result.Value.Logs) Console.WriteLine(log);
                    Console.WriteLine("=== end logs ===");
                }

                // Optional: fail fast if simulation returned an error object
                if (sim.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(sim.Result.Value.Error)}");
            }

            var sig = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new SellShareResult
            {
                MarketPubkey = marketPk.Key,
                UserCollateralAta = userCollateralPk.Key,
                SharesIn = sharesIn,
                MinCollateralOut = minCollateralOut,
                OutcomeIndex = outcomeIndex,
                TransactionSignature = sig
            };
        }


        /// <summary>
        /// Claims winnings for the current (MVP) user = authority.
        /// This matches the Anchor ClaimWinnings accounts exactly:
        /// market, position PDA, user, user_collateral_ata, vault, authority, token_program.
        /// </summary>
        public async Task<string> ClaimWinningsAsync(
            string marketPubkey,
            string userCollateralAta,
            CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);
            var userCollateral = new PublicKey(userCollateralAta);
            // vault PDA: seeds = [b"vault_v2", market.key()]
            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };

            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");
            
            // vault_authority PDA: seeds = [b"vault_auth_v2", market.key()]
            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };

            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");
            
            // In the Rust program, seeds = [b"position_v2", market.key(), user.key()]
            // For MVP, user == authority.
            byte[][] seeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                _authority.PublicKey.KeyBytes
            };

            if (!PublicKey.TryFindProgramAddress(seeds, _programId, out var positionPk, out var bumpSeed))
                throw new Exception($"Failed to derive position PDA.");

            // ----- Build ix data: Anchor discriminator for `claim_winnings` -----
            // Anchor discriminator = first 8 bytes of sha256("global:claim_winnings_v2")
            var ixData = GetAnchorDiscriminator("global", "claim_winnings_v2");

            // ----- Get fresh blockhash -----
            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            // ----- Accounts: must match Rust order -----
            var accounts = new List<AccountMeta>
            {
                // 0. market (mut)
                AccountMeta.Writable(marketPk, false),

                // 1. vault (mut)
                AccountMeta.Writable(vaultPk, false),

                // 2. vault_authority (read-only, NOT signer)
                AccountMeta.ReadOnly(vaultAuthorityPk, false),

                // 3. position PDA (mut)
                AccountMeta.Writable(positionPk, false),

                // 4. user (mut, signer)
                AccountMeta.Writable(_authority.PublicKey, true),

                // 5. user_collateral_ata (mut)
                AccountMeta.Writable(userCollateral, false),

                // 6. token_program
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            };

            var ix = new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = accounts,
                Data = ixData
            };

            // ----- Build & sign transaction -----
            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(recentBlockhash)
                .SetFeePayer(_authority.PublicKey);

            foreach (var ixBudget in BuildComputeBudgetIxs())
            {
                txBuilder.AddInstruction(ixBudget);
            }

            txBuilder.AddInstruction(ix);
            var tx = txBuilder.Build(new[] { _authority });   // authority signs as both user & authority

            // Optional: simulate for logs
            if (_cfg.SimulateBeforeSend)
            {
                var simResult = await _rpc.SimulateTransactionAsync(tx);
                if (!simResult.WasSuccessful || simResult.Result?.Value == null)
                    throw new Exception("Simulation failed: " + simResult.Reason);

                if (simResult.Result.Value.Logs != null)
                {
                    Console.WriteLine("=== ClaimWinnings simulation logs ===");
                    foreach (var log in simResult.Result.Value.Logs)
                        Console.WriteLine(log);
                    Console.WriteLine("=== End logs ===");
                }
            }
            
            var sendResult = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);
            return sendResult; // transaction signature
        }

        private static byte[] BuildCreateMarketInstructionData(
            ulong marketId,
            string question,
            DateTime endTimeUtc,
            ulong initialLiquidity)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "create_market_cpmm"));

            bw.Write(marketId); // u64 LE
            WriteBorshString(bw, question);

            long endTime = new DateTimeOffset(endTimeUtc.ToUniversalTime()).ToUnixTimeSeconds();
            bw.Write(endTime); // i64 LE

            bw.Write(initialLiquidity); // u64 LE

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

        private static byte[] BuildBuySharesInstructionData(byte outcomeIndex, ulong maxCollateralIn, ulong minSharesOut)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "buy_shares"));
            bw.Write(outcomeIndex);       // u8
            bw.Write(maxCollateralIn);    // u64
            bw.Write(minSharesOut);       // u64

            bw.Flush();
            return ms.ToArray();
        }
        
        private static byte[] BuildSellSharesInstructionData(byte outcomeIndex, ulong sharesIn, ulong minCollateralOut)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "sell_shares"));
            bw.Write(outcomeIndex);     // u8
            bw.Write(sharesIn);         // u64
            bw.Write(minCollateralOut); // u64

            bw.Flush();
            return ms.ToArray();
        }
        
        private IEnumerable<TransactionInstruction> BuildComputeBudgetIxs()
        {
            yield return ComputeBudgetProgram.SetComputeUnitLimit(_cfg.ComputeUnitLimit);

            if (_cfg.ComputeUnitPriceMicroLamports > 0)
            {
                yield return ComputeBudgetProgram.SetComputeUnitPrice(
                    _cfg.ComputeUnitPriceMicroLamports
                );
            }
        }

    }
}
