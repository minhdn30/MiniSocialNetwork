using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        /// Saves all changes made in this unit of work to the database.
        Task CommitAsync();
        
        /// Begins a new database transaction.
        Task<IDbContextTransaction> BeginTransactionAsync();
        
        /// Executes a transactional operation with automatic commit/rollback.
        /// If the operation fails, the transaction is rolled back and orphaned resources can be cleaned up.
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation, 
            Func<Task>? onRollback = null);
    }
}
