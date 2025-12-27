using MarketService.Domain.Entities;
using MarketService.Domain.Interface;
using MarketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketService.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly MarketDbContext _db;

    public UnitOfWork(MarketDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
    }
}