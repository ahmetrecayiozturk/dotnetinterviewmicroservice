using common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace PaymentService.Infrastructure;
public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(PaymentDbContext context) => _context = context;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveAsync(cancellationToken);
            await _transaction!.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Dispose() => _transaction?.Dispose();
}