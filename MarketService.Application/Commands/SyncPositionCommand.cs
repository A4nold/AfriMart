namespace MarketService.Application.Commands;

public sealed record SyncPositionCommand(Guid UserId, string MarketPubkey);