using Microsoft.EntityFrameworkCore.Storage;
using SocialNetwork.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private bool _disposed = false;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public async Task CommitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return _context.Database.BeginTransactionAsync();
        }

        /// <inheritdoc />
        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation,
            Func<Task>? onRollback = null)
        {
            using var transaction = await BeginTransactionAsync();
            try
            {
                var result = await operation();
                await CommitAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                
                // Execute cleanup callback if provided (e.g., delete orphaned cloud resources)
                if (onRollback != null)
                {
                    try
                    {
                        await onRollback();
                    }
                    catch
                    {
                        // Swallow cleanup errors to not mask the original exception
                    }
                }
                
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
