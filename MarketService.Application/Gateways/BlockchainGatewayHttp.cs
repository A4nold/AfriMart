using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MarketService.Application.Dtos;
using MarketService.Application.Exception;
using MarketService.Application.Interfaces;
using MarketService.Application.Requests;
using MarketService.Application.Responses;
using Microsoft.Extensions.Options;
using Solnet.Wallet;
using MarketService.Domain.Interface;
using Solnet.KeyStore;

namespace MarketService.Application.Gateways;

public sealed class BlockchainGatewayOptions
{
    public string BaseUrl { get; init; } = default!;
    public string ProgramId { get; init; } = default!;
    public string AuthorityPubKey { get; init; } = default!;
}

public sealed class BlockchainGatewayHttp : IBlockchainGateway
{
    private readonly HttpClient _http;
    private readonly BlockchainGatewayOptions _opts;
    private readonly PublicKey _authorityPublicKey;
    private readonly PublicKey _programId;
    
    
    public BlockchainGatewayHttp(HttpClient http, IOptions<BlockchainGatewayOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
        
        
        _programId = new PublicKey(_opts.ProgramId);
        _authorityPublicKey = new PublicKey(_opts.AuthorityPubKey);
    }

    public string ProgramId => _programId.Key;
    public string AuthorityPubKey => _authorityPublicKey.Key;

    public MarketPdas DeriveMarketPdas(ulong marketSeedId)
    {
        var seedBytes = BitConverter.GetBytes(marketSeedId);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(seedBytes);

        var marketSeeds = new[]
        {
            Encoding.UTF8.GetBytes("market_v2"),
            _authorityPublicKey.KeyBytes,
            seedBytes
        };

        if (!PublicKey.TryFindProgramAddress(marketSeeds, _programId, out var marketPk, out _))
            throw new InvalidOperationException("Failed to derive market PDA.");

        var vaultSeeds = new[]
        {
            Encoding.UTF8.GetBytes("vault_v2"),
            marketPk.KeyBytes
        };

        if (!PublicKey.TryFindProgramAddress(vaultSeeds, _programId, out var vaultPk, out _))
            throw new InvalidOperationException("Failed to derive vault PDA.");

        var vaultAuthSeeds = new[]
        {
            Encoding.UTF8.GetBytes("vault_auth_v2"),
            marketPk.KeyBytes
        };

        if (!PublicKey.TryFindProgramAddress(vaultAuthSeeds, _programId, out var vaultAuthPk, out _))
            throw new InvalidOperationException("Failed to derive vault authority PDA.");

        return new MarketPdas(
            MarketPubKey: marketPk.Key,
            VaultPubKey: vaultPk.Key,
            VaultAuthorityPubKey: vaultAuthPk.Key
        );
    }


    public async Task<BlockchainCreateMarketResponse> CreateMarketAsync(BlockchainCreateMarketRequest req, CancellationToken ct)
    {
        // BlockchainService: POST /api/markets/create
        var res = await _http.PostAsJsonAsync("/api/markets/create", new
        {
            marketId = req.MarketId,
            question = req.Question,
            endTime = req.EndTimeUtc,
            initialLiquidity = req.InitialLiquidity
        }, ct);
        
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new System.Exception($"HTTP {res.StatusCode}: {body}");
        }

        return await ReadOrThrowAsync<BlockchainCreateMarketResponse>(res, ct);
    }

    public async Task<BlockchainResolveMarketResponse> ResolveMarketAsync(BlockchainResolveMarketRequest req, CancellationToken ct)
    {
        // BlockchainService: POST /api/markets/{marketPubkey}/resolve
        var res = await _http.PostAsJsonAsync($"/api/markets/{req.MarketPubkey}/resolve", new
        {
            winningOutcomeIndex = req.WinningOutcomeIndex
        }, ct);

        return await ReadOrThrowAsync<BlockchainResolveMarketResponse>(res, ct);
    }

    public async Task<BlockchainBuyResponse> BuySharesAsync(BlockchainBuyRequest req, CancellationToken ct)
    {
        // BlockchainService: POST /api/markets/{marketPubkey}/buy
        var res = await _http.PostAsJsonAsync($"/api/markets/{req.MarketPubkey}/buy", new
        {
            marketPubKey = req.MarketPubkey,
            maxCollateralIn = req.MaxCollateralIn,
            minSharesOut = req.MinSharesOut,
            outcomeIndex = req.OutcomeIndex
        }, ct);

        return await ReadOrThrowAsync<BlockchainBuyResponse>(res, ct);
    }

    public async Task<BlockchainSellResponse> SellSharesAsync(BlockchainSellRequest req, CancellationToken ct)
    {
        // BlockchainService: POST /api/markets/{marketPubkey}/sell
        var res = await _http.PostAsJsonAsync($"/api/markets/{req.MarketPubkey}/sell", new
        {
            sharesIn = req.SharesIn,
            minCollateralOut = req.MinCollateralOut,
            outcomeIndex = req.OutcomeIndex
        }, ct);

        return await ReadOrThrowAsync<BlockchainSellResponse>(res, ct);
    }

    public async Task<BlockchainClaimResponse> ClaimWinningsAsync(BlockchainClaimRequest req, CancellationToken ct)
    {
        // BlockchainService: POST /api/markets/{marketPubkey}/claim
        var res = await _http.PostAsync($"/api/markets/{req.MarketPubkey}/claim", content: null, ct);

        // Your controller returns an anonymous object { MarketPubkey, TransactionSignature }
        var dto = await ReadOrThrowAsync<ClaimDto>(res, ct);

        return new BlockchainClaimResponse(dto.MarketPubkey,dto.TransactionSignature);
    }

    public async Task<GetPositionResponse> GetPositionAsync(string marketPubKey, string ownerPubKey, CancellationToken ct)
    {
        // BlockchainService: GET /api/markets/{marketPubkey}/positions/{ownerPubkey}
        var res = await _http.GetAsync($"/api/markets/{marketPubKey}/positions/{ownerPubKey}", ct);

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new System.Exception($"HTTP {res.StatusCode}: {body}");
        }

        return await ReadOrThrowAsync<GetPositionResponse>(res, ct);
    }

    public async Task<MarketStateResponse> GetMarketAsync(string marketPubkey, CancellationToken ct)
    {
        // BlockchainService: GET /api/markets/{marketPubkey}/state
        var res = await _http.GetAsync($"/api/markets/{marketPubkey}/state", ct);
        
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new System.Exception($"HTTP {res.StatusCode}: {body}");
        }

        return await ReadOrThrowAsync<MarketStateResponse>(res, ct);
    }
    
    private sealed record ClaimDto(string MarketPubkey, string TransactionSignature);

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage res, CancellationToken ct)
    {
        // If BlockchainService returns { code, message, anchor } we want to preserve it
        var body = await res.Content.ReadAsStringAsync(ct);

        if (res.IsSuccessStatusCode)
        {
            // try deserialize
            var obj = JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return obj ?? throw new System.Exception("BlockchainService returned empty response body.");
        }

        // Map common statuses into something MarketService already understands
        if (res.StatusCode == HttpStatusCode.Conflict)
            throw new ConflictException($"Blockchain conflict: {body}");

        if (res.StatusCode == HttpStatusCode.BadRequest)
            throw new ValidationException($"Blockchain bad request: {body}");

        throw new ExternalDependencyException($"BlockchainService error ({(int)res.StatusCode}): {body}");
    }
}
