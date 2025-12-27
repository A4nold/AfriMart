namespace MarketService.Application.Dtos;

public sealed record MarketPdas(
    string MarketPubKey,
    string VaultPubKey,
    string VaultAuthorityPubKey
);

public sealed record CreateMarketIdemPayload(ulong MarketSeedId);
