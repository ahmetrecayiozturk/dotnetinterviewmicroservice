using common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ShippingService.Infrastructure;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly ShippingDbContext _dbContext;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(ShippingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Önce tüm deðiþiklikleri veritabanýna kaydediyoruz
            await SaveAsync(cancellationToken);

            // Sonra transaction'ý baþarýlý olarak iþaretliyoruz
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            // Hata durumunda rollback yapýyoruz
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            // Transaction nesnesini mutlaka temizliyoruz
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        if (_currentTransaction != null)
        {
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
    }
}