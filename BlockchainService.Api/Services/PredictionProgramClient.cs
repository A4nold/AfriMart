using Microsoft.Extensions.Options;
using Solnet.KeyStore;
using Solnet.Programs;

using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using BlockchainService.Api.Dto;
using BlockchainService.Api.Exceptions;
using BlockchainService.Domain.Helpers;

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

            // For typical id.json with no passphrase
            var wallet = keystore.RestoreKeystore(json);

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

        private static (string? code, int? number) TryParseAnchorError(string message)
        {
            string? code = null;
            int? number = null;

            var codeMarker = "Error Code:";
            var numMarker = "Error Number:";

            var codeIdx = message.IndexOf(codeMarker, StringComparison.OrdinalIgnoreCase);
            if (codeIdx >= 0)
            {
                var after = message[(codeIdx + codeMarker.Length)..].TrimStart();
                var end = after.IndexOf('.', StringComparison.Ordinal);
                if (end > 0) code = after[..end].Trim();
            }

            var numIdx = message.IndexOf(numMarker, StringComparison.OrdinalIgnoreCase);
            if (numIdx >= 0)
            {
                var after = message[(numIdx + numMarker.Length)..].TrimStart();
                var end = after.IndexOf('.', StringComparison.Ordinal);
                if (end > 0 && int.TryParse(after[..end].Trim(), out var n)) number = n;
            }

            return (code, number);
        }

        // ⭐ SUGGESTION: If send fails, logs are usually in simulation, not in send.Reason.
        private async Task<(bool ok, string? logs)> TrySimulateForLogsAsync(byte[] tx, CancellationToken ct)
        {
            try
            {
                var sim = await _rpc.SimulateTransactionAsync(tx);
                if (!sim.WasSuccessful || sim.Result?.Value == null)
                    return (false, null);

                var logs = sim.Result.Value.Logs != null
                    ? string.Join("\n", sim.Result.Value.Logs)
                    : null;

                return (true, logs);
            }
            catch
            {
                return (false, null);
            }
        }

        private async Task<string> SendAndConfirmAsync(byte[] tx, Commitment commitment, bool skipPreflight, CancellationToken ct = default)
        {
            var send = await _rpc.SendTransactionAsync(tx, skipPreflight: skipPreflight, commitment: commitment);
            if (!send.WasSuccessful || string.IsNullOrWhiteSpace(send.Result))
            {
                // ⭐ SUGGESTION: try to fetch sim logs for AnchorError context
                var (_, logs) = await TrySimulateForLogsAsync(tx, ct);

                var msg = $"SendTransaction failed: {send.Reason}";
                if (!string.IsNullOrWhiteSpace(logs))
                    msg += "\n--- Simulation logs ---\n" + logs;

                var (anchorCode, anchorNumber) = TryParseAnchorError(msg);
                throw new AnchorProgramException(msg, anchorCode, anchorNumber);
            }

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

            for (var i = 0; i < 60; i++)
            {
                ct.ThrowIfCancellationRequested();

                var status = await _rpc.GetSignatureStatusesAsync(new List<string> { sig }, searchTransactionHistory: true);
                if (status.WasSuccessful && status.Result?.Value != null)
                {
                    var s = status.Result.Value.FirstOrDefault();
                    if (s != null)
                    {
                        if (s.Error != null)
                            throw new Exception($"Transaction failed on-chain: {JsonSerializer.Serialize(s.Error)}");

                        if (IsAcceptable(s.ConfirmationStatus))
                            return sig;
                    }
                }

                await Task.Delay(500, ct);
            }

            throw new Exception($"Transaction not confirmed in time. Signature: {sig}");
        }

        // -----------------------------
        // Create Market
        // -----------------------------
        public async Task<MarketResult> CreateMarketAsync(
            ulong marketId,
            string question,
            DateTime endTimeUtc,
            ulong initialLiquidity,
            CancellationToken ct = default)
        {
            var collateralMintPk = new PublicKey(_cfg.FUSD);
            var authorityPk = _authority.PublicKey;

            var (authorityAta, createAuthorityAtaIx) =
                await EnsureAtaIxAsync(authorityPk, collateralMintPk, authorityPk, ct);

            var marketIdBytes = BitConverter.GetBytes(marketId);
            if (!BitConverter.IsLittleEndian) Array.Reverse(marketIdBytes);

            if (initialLiquidity == 0)
                throw new ArgumentException("initialLiquidity must be > 0", nameof(initialLiquidity));

            byte[][] marketSeeds =
            {
                Encoding.UTF8.GetBytes("market_v2"),
                authorityPk.KeyBytes,
                marketIdBytes
            };
            if (!PublicKey.TryFindProgramAddress(marketSeeds, _programId, out var marketPk, out _))
                throw new Exception("Failed to derive market PDA.");

            var existing = await _rpc.GetAccountInfoAsync(marketPk, commitment: Commitment.Confirmed);
            if (existing.WasSuccessful && existing.Result?.Value != null)
                throw new Exception($"Market already exists for marketId={marketId}. Market PDA: {marketPk.Key}");

            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            var ixData = BuildCreateMarketInstructionData(marketId, question, endTimeUtc, initialLiquidity);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception("Failed to get recent blockhash: " + blockhashResult.Reason);

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPk, false),
                AccountMeta.Writable(vaultPk, false),
                AccountMeta.ReadOnly(vaultAuthorityPk, false),
                AccountMeta.ReadOnly(collateralMintPk, false),
                AccountMeta.Writable(authorityPk, true),
                AccountMeta.Writable(authorityAta, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
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
                .SetFeePayer(authorityPk);

            foreach (var ixBudget in BuildComputeBudgetIxs())
                txBuilder.AddInstruction(ixBudget);

            if (createAuthorityAtaIx != null)
                txBuilder.AddInstruction(createAuthorityAtaIx);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

            if (_cfg.SimulateBeforeSend)
            {
                var simResult = await _rpc.SimulateTransactionAsync(tx);
                if (!simResult.WasSuccessful || simResult.Result?.Value == null)
                    throw new Exception($"Simulation Failed: {simResult.Reason}");

                if (simResult.Result.Value.Logs != null)
                {
                    Console.WriteLine("=== Simulation Logs ===");
                    foreach (var log in simResult.Result.Value.Logs)
                        Console.WriteLine(log);
                    Console.WriteLine("=== End logs ===");
                }

                if (simResult.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(simResult.Result.Value.Error)}");
            }

            var txSignature = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new MarketResult
            {
                MarketAction = "Create Market Action",
                MarketPubkey = marketPk.Key,
                TransactionSignature = txSignature
            };
        }

        // -----------------------------
        // Resolve Market (✅ CHANGED: include vault)
        // -----------------------------
        public async Task<MarketResult> ResolveMarketAsync(
            string marketPubkey,
            byte outcomeIndex,
            CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);

            // ✅ CHANGED: resolve_market now snapshots vault.amount, so vault must be provided
            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            var ixData = BuildResolveMarketInstructionData(outcomeIndex);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception("Failed to get latest blockhash: " + blockhashResult.Reason);

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            // ✅ CHANGED: account order must match ResolveMarketV2: market, vault, authority
            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPk, false),
                AccountMeta.Writable(vaultPk, false),                // ✅ NEW
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
                txBuilder.AddInstruction(ixBudget);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

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

                if (simResult.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(simResult.Result.Value.Error)}");
            }

            var txSignature = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new MarketResult
            {
                MarketAction = "Resolve Market",
                MarketPubkey = marketPk.Key,
                TransactionSignature = txSignature
            };
        }

        // -----------------------------
        // Buy Shares (MVP user = authority)
        // -----------------------------
        public async Task<BuyShareResult> BuySharesAsync(
            string marketPubkey,
            ulong maxCollateralIn,
            ulong minSharesOut,
            byte outcomeIndex,
            CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);

            var collateralMintPk = await GetMarketCollateralMintAsync(marketPk, ct);

            var userPk = _authority.PublicKey; // MVP user = authority

            var (userCollateralAtaPk, createUserAtaIx) =
                await EnsureAtaIxAsync(userPk, collateralMintPk, feePayer: _authority.PublicKey, ct);

            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                userPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            var ixData = BuildBuySharesInstructionData(outcomeIndex, maxCollateralIn, minSharesOut);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPk, false),
                AccountMeta.Writable(vaultPk, false),
                AccountMeta.ReadOnly(vaultAuthorityPk, false),
                AccountMeta.Writable(positionPk, false),
                AccountMeta.Writable(userPk, true),
                AccountMeta.Writable(userCollateralAtaPk, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
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

            if (createUserAtaIx != null)
                txBuilder.AddInstruction(createUserAtaIx);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

            if (_cfg.SimulateBeforeSend)
            {
                var sim = await _rpc.SimulateTransactionAsync(tx);
                if (!sim.WasSuccessful || sim.Result?.Value == null)
                    throw new Exception("Simulation failed: " + sim.Reason);

                if (sim.Result.Value.Logs != null)
                    foreach (var log in sim.Result.Value.Logs) Console.WriteLine(log);

                if (sim.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(sim.Result.Value.Error)}");
            }

            var sig = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new BuyShareResult
            {
                MarketPubkey = marketPk.Key,
                UserCollateralAta = userCollateralAtaPk.Key,
                MaxCollateralIn = maxCollateralIn,
                MinSharesOut = minSharesOut,
                OutcomeIndex = outcomeIndex,
                TransactionSignature = sig
            };
        }

        // -----------------------------
        // Sell Shares (MVP user = authority)
        // -----------------------------
        public async Task<SellShareResult> SellSharesAsync(
            string marketPubkey,
            ulong sharesIn,
            ulong minCollateralOut,
            byte outcomeIndex,
            CancellationToken ct = default)
        {
            if (outcomeIndex > 1) throw new ArgumentOutOfRangeException(nameof(outcomeIndex), "OutcomeIndex must be 0 or 1.");
            if (sharesIn == 0) throw new ArgumentException("SharesIn must be > 0", nameof(sharesIn));

            var marketPk = new PublicKey(marketPubkey);
            var userPk = _authority.PublicKey;

            var collateralMintPk = await GetMarketCollateralMintAsync(marketPk, ct);

            var (userCollateralAtaPk, createUserAtaIx) =
                await EnsureAtaIxAsync(userPk, collateralMintPk, feePayer: _authority.PublicKey, ct);

            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                userPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            var ixData = BuildSellSharesInstructionData(outcomeIndex, sharesIn, minCollateralOut);

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPk, false),
                AccountMeta.Writable(vaultPk, false),
                AccountMeta.ReadOnly(vaultAuthorityPk, false),
                AccountMeta.Writable(positionPk, false),
                AccountMeta.Writable(userPk, true),
                AccountMeta.Writable(userCollateralAtaPk, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false)
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

            if (createUserAtaIx != null)
                txBuilder.AddInstruction(createUserAtaIx);

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

                if (sim.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(sim.Result.Value.Error)}");
            }

            var sig = await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);

            return new SellShareResult
            {
                MarketPubkey = marketPk.Key,
                UserCollateralAta = userCollateralAtaPk.Key,
                SharesIn = sharesIn,
                MinCollateralOut = minCollateralOut,
                OutcomeIndex = outcomeIndex,
                TransactionSignature = sig
            };
        }

        // -----------------------------
        // Claim Winnings (adds snapshot sanity check)
        // -----------------------------
        public async Task<string> ClaimWinningsAsync(string marketPubkey, CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);
            var userPk = _authority.PublicKey;

            // ⭐ SUGGESTION: sanity check classic payout snapshots exist
            var (slot, marketState) = await GetMarketAsync(marketPk, ct);
            if (marketState.Status != 1) // Resolved
                throw new Exception($"Market not resolved. status={marketState.Status} winning={marketState.WinningOutcome}");

            if (marketState.ResolvedVaultBalance == 0 || marketState.ResolvedTotalWinningShares == 0)
                throw new Exception("Market snapshots are not set (resolved_vault_balance / resolved_total_winning_shares). Ensure resolve_market was called with vault account.");

            var collateralMintPk = marketState.CollateralMint;

            var (userCollateralAtaPk, createUserAtaIx) =
                await EnsureAtaIxAsync(userPk, collateralMintPk, feePayer: _authority.PublicKey, ct);

            byte[][] vaultSeeds =
            {
                Encoding.UTF8.GetBytes("vault_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
                throw new Exception("Failed to derive vault PDA.");

            byte[][] vaultAuthSeeds =
            {
                Encoding.UTF8.GetBytes("vault_auth_v2"),
                marketPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthorityPk, out _))
                throw new Exception("Failed to derive vault_authority PDA.");

            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                userPk.KeyBytes
            };
            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            var ixData = GetAnchorDiscriminator("global", "claim_winnings_v2");

            var blockhashResult = await _rpc.GetLatestBlockHashAsync();
            if (!blockhashResult.WasSuccessful || blockhashResult.Result == null)
                throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");

            var recentBlockhash = blockhashResult.Result.Value.Blockhash;

            var accounts = new List<AccountMeta>
            {
                AccountMeta.Writable(marketPk, false),
                AccountMeta.Writable(vaultPk, false),
                AccountMeta.ReadOnly(vaultAuthorityPk, false),
                AccountMeta.Writable(positionPk, false),
                AccountMeta.Writable(userPk, true),
                AccountMeta.Writable(userCollateralAtaPk, false),
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

            if (createUserAtaIx != null)
                txBuilder.AddInstruction(createUserAtaIx);

            txBuilder.AddInstruction(ix);

            var tx = txBuilder.Build(new[] { _authority });

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

                if (simResult.Result.Value.Error != null)
                    throw new Exception($"Simulation error: {JsonSerializer.Serialize(simResult.Result.Value.Error)}");
            }

            return await SendAndConfirmAsync(tx, GetCommitment(_cfg.Commitment), _cfg.SkipPreflight, ct);
        }

        // -----------------------------
        // Instruction data builders
        // -----------------------------
        private static byte[] BuildCreateMarketInstructionData(
            ulong marketId,
            string question,
            DateTime endTimeUtc,
            ulong initialLiquidity)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "create_market_cpmm"));
            bw.Write(marketId);
            WriteBorshString(bw, question);

            long endTime = new DateTimeOffset(endTimeUtc.ToUniversalTime()).ToUnixTimeSeconds();
            bw.Write(endTime);
            bw.Write(initialLiquidity);

            bw.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildResolveMarketInstructionData(byte outcomeIndex)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "resolve_market"));
            bw.Write(outcomeIndex);

            bw.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildBuySharesInstructionData(byte outcomeIndex, ulong maxCollateralIn, ulong minSharesOut)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "buy_shares"));
            bw.Write(outcomeIndex);
            bw.Write(maxCollateralIn);
            bw.Write(minSharesOut);

            bw.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildSellSharesInstructionData(byte outcomeIndex, ulong sharesIn, ulong minCollateralOut)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(GetAnchorDiscriminator("global", "sell_shares"));
            bw.Write(outcomeIndex);
            bw.Write(sharesIn);
            bw.Write(minCollateralOut);

            bw.Flush();
            return ms.ToArray();
        }

        // -----------------------------
        // Decode helpers
        // -----------------------------
        private static byte[] GetAnchorDiscriminator(string ns, string name)
        {
            var input = $"{ns}:{name}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return hash.Take(8).ToArray();
        }

        private static void WriteBorshString(BinaryWriter bw, string value)
        {
            // ✅ Anchor/Borsh string = u32 length (LE) + bytes
            var bytes = Encoding.UTF8.GetBytes(value);
            bw.Write((uint)bytes.Length); // ✅ CHANGED: was (int)
            bw.Write(bytes);
        }
        
        private IEnumerable<TransactionInstruction> BuildComputeBudgetIxs()
        {
            yield return ComputeBudgetProgram.SetComputeUnitLimit(_cfg.ComputeUnitLimit);

            if (_cfg.ComputeUnitPriceMicroLamports > 0)
            {
                yield return ComputeBudgetProgram.SetComputeUnitPrice(_cfg.ComputeUnitPriceMicroLamports);
            }
        }

        private PublicKey DeriveAta(PublicKey owner, PublicKey mint)
            => AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint);

        private async Task<(PublicKey Ata, TransactionInstruction? CreateIx)> EnsureAtaIxAsync(
            PublicKey owner,
            PublicKey mint,
            PublicKey feePayer,
            CancellationToken ct = default)
        {
            var ata = DeriveAta(owner, mint);

            var acc = await _rpc.GetAccountInfoAsync(ata, commitment: Commitment.Confirmed);
            if (acc.WasSuccessful && acc.Result?.Value != null)
                return (ata, null);

            var ix = AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                payer: feePayer,
                owner: owner,
                mint: mint
            );

            return (ata, ix);
        }

        // -----------------------------
        // Market decode (full enough for classic pro-rata)
        // -----------------------------
        public sealed record MarketV2State(
            ulong MarketId,
            PublicKey Authority,
            string Question,
            PublicKey CollateralMint,
            PublicKey Vault,
            long EndTime,
            byte Status,
            sbyte WinningOutcome,
            ulong YesPool,
            ulong NoPool,
            ulong TotalYesShares,
            ulong TotalNoShares,
            ulong ResolvedVaultBalance,
            ulong ResolvedTotalWinningShares
        );

        private static readonly byte[] MarketV2Discriminator = ComputeAnchorDiscriminator("MarketV2");

        private static byte[] ComputeAnchorDiscriminator(string accountName)
        {
            using var sha = SHA256.Create();
            var preimage = Encoding.UTF8.GetBytes($"account:{accountName}");
            var hash = sha.ComputeHash(preimage);
            return hash.Take(8).ToArray();
        }

        public async Task<(ulong Slot, MarketV2State)> GetMarketAsync(PublicKey marketPk, CancellationToken ct = default)
        {
            var resp = await _rpc.GetAccountInfoAsync(marketPk, commitment: Commitment.Confirmed);
            if (!resp.WasSuccessful || resp.Result?.Value == null)
                throw new Exception($"Failed to fetch market account: {resp.Reason}");

            var dataField = resp.Result.Value.Data;
            if (dataField == null || dataField.Count == 0 || string.IsNullOrWhiteSpace(dataField[0]))
                throw new Exception("Market account data is empty.");

            var raw = Convert.FromBase64String(dataField[0]);
            var state =  DecodeMarketV2(raw);

            var slot = resp.Result.Context.Slot;
            return (slot, state);
        }

        private static MarketV2State DecodeMarketV2(byte[] raw)
        {
            // Anchor account layout:
            // 8   discriminator
            // 8   u64 market_id
            // 32  Pubkey authority
            // 4   u32 question_len
            // N   question bytes
            // 32  Pubkey collateral_mint
            // 32  Pubkey vault
            // 8   i64 end_time
            // 1   u8 status
            // 1   i8 winning_outcome
            // 8*6 u64: yes_pool, no_pool, total_yes_shares, total_no_shares, resolved_vault_balance, resolved_total_winning_shares

            const int minHeader = 8 + 8 + 32 + 4;
            if (raw.Length < minHeader)
                throw new Exception($"Market account data too small. Len={raw.Length}, need>={minHeader}");

            // bytes after the question string
            const int fixedAfterQuestion =
                32 + // collateral_mint
                32 + // vault
                8  + // end_time
                1  + // status
                1  + // winning_outcome
                (8 * 6); // pools/shares/snapshots

            using var ms = new MemoryStream(raw, writable: false);
            using var br = new BinaryReader(ms);

            var disc = br.ReadBytes(8);
            if (!disc.SequenceEqual(MarketV2Discriminator))
                throw new Exception("Account discriminator mismatch; not a MarketV2 account.");

            var marketId = br.ReadUInt64();
            var authority = new PublicKey(br.ReadBytes(32));

            // ✅ Anchor/Borsh string length is u32
            var qLen = br.ReadUInt32();

            // ✅ CHANGED: align to #[max_len(256)] rather than an arbitrary huge number
            if (qLen > 256)
                throw new Exception($"Market question length exceeds max_len(256): {qLen}");

            long remaining = raw.Length - ms.Position;
            long needed = (long)qLen + fixedAfterQuestion;

            // ✅ CHANGED: explicit truncation check against the fixed tail size
            if (remaining < needed)
                throw new Exception($"Market data truncated. Remaining={remaining}, need={needed}");

            var question = Encoding.UTF8.GetString(br.ReadBytes((int)qLen));

            var collateralMint = new PublicKey(br.ReadBytes(32));
            var vault = new PublicKey(br.ReadBytes(32));
            var endTime = br.ReadInt64();
            var status = br.ReadByte();

            // ReadSByte already returns sbyte (i8)
            var winningOutcome = br.ReadSByte();

            var yesPool = br.ReadUInt64();
            var noPool = br.ReadUInt64();
            var totalYesShares = br.ReadUInt64();
            var totalNoShares = br.ReadUInt64();

            var resolvedVaultBalance = br.ReadUInt64();
            var resolvedTotalWinningShares = br.ReadUInt64();

            return new MarketV2State(
                MarketId: marketId,
                Authority: authority,
                Question: question,
                CollateralMint: collateralMint,
                Vault: vault,
                EndTime: endTime,
                Status: status,
                WinningOutcome: winningOutcome,
                YesPool: yesPool,
                NoPool: noPool,
                TotalYesShares: totalYesShares,
                TotalNoShares: totalNoShares,
                ResolvedVaultBalance: resolvedVaultBalance,
                ResolvedTotalWinningShares: resolvedTotalWinningShares
            );
        }


        // Your small helper used by buy/sell/claim
        private async Task<PublicKey> GetMarketCollateralMintAsync(PublicKey marketPk, CancellationToken ct = default)
        {
            var (slot, market) = await GetMarketAsync(marketPk, ct);
            return market.CollateralMint;
        }

        // -----------------------------
        // Position decode + GetPositionAsync
        // -----------------------------
        private sealed record DecodedPosition(string Market, string Owner, ulong YesShares, ulong NoShares, bool Claimed);

        private static readonly byte[] PositionV2Discriminator = ComputeAnchorDiscriminator("PositionV2"); // ⭐ SUGGESTION

        private static DecodedPosition DecodePositionV2(byte[] raw)
        {
            const int disc = 8;
            const int need = disc + 32 + 32 + 8 + 8 + 1;

            if (raw.Length < need)
                throw new Exception($"Position data too small. Expected >= {need}, got {raw.Length}.");

            using var ms = new MemoryStream(raw, writable: false);
            using var br = new BinaryReader(ms);

            var d = br.ReadBytes(8);
            if (!d.SequenceEqual(PositionV2Discriminator))
                throw new Exception("Account discriminator mismatch; not a PositionV2 account.");

            var marketBytes = br.ReadBytes(32);
            var ownerBytes = br.ReadBytes(32);

            var yes = br.ReadUInt64();
            var no = br.ReadUInt64();

            // ✅ CHANGED: Anchor bool is u8 (0 or 1). Be strict.
            var claimedByte = br.ReadByte();
            if (claimedByte > 1)
                throw new Exception($"Invalid bool encoding for claimed: {claimedByte} (expected 0 or 1).");

            var claimed = claimedByte == 1;

            return new DecodedPosition(
                Market: new PublicKey(marketBytes).Key,
                Owner: new PublicKey(ownerBytes).Key,
                YesShares: yes,
                NoShares: no,
                Claimed: claimed
            );
        }

        public async Task<GetPositionOnChain> GetPositionAsync(
            string marketPubkey,
            string ownerPubkey,
            CancellationToken ct = default)
        {
            var marketPk = new PublicKey(marketPubkey);
            var ownerPk = new PublicKey(ownerPubkey);

            byte[][] positionSeeds =
            {
                Encoding.UTF8.GetBytes("position_v2"),
                marketPk.KeyBytes,
                ownerPk.KeyBytes
            };

            if (!PublicKey.TryFindProgramAddress(positionSeeds, _programId, out var positionPk, out _))
                throw new Exception("Failed to derive position PDA.");

            var resp = await _rpc.GetAccountInfoAsync(positionPk, commitment: Commitment.Confirmed);

            var slot = resp.Result?.Context?.Slot;
            if (slot is null)
                throw new Exception("Failed to derive position Slot.");

            if (!resp.WasSuccessful)
                throw new Exception($"Failed to fetch position account: {resp.Reason}");

            if (resp.Result?.Value == null)
                throw new Exception("Position account not found (user has not participated in this market).");

            var dataField = resp.Result.Value.Data;
            if (dataField == null || dataField.Count == 0 || string.IsNullOrWhiteSpace(dataField[0]))
                throw new Exception("Position account data is empty.");

            var raw = Convert.FromBase64String(dataField[0]);

            var decoded = DecodePositionV2(raw);

            if (decoded.Market != marketPk.Key || decoded.Owner != ownerPk.Key)
                throw new Exception("Decoded position does not match requested market/owner.");

            return new GetPositionOnChain(
                MarketPubkey: decoded.Market,
                OwnerPubkey: decoded.Owner,
                PositionPubkey: positionPk.Key,
                YesShares: decoded.YesShares,
                NoShares: decoded.NoShares,
                Claimed: decoded.Claimed,
                LastSyncedSlot: slot.Value
            );
        }
    }
}
