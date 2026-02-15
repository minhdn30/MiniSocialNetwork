using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Repositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Saves all changes made in this unit of work to the database.
        /// </summary>
        Task CommitAsync();
        
        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        Task<IDbContextTransaction> BeginTransactionAsync();
        
        /// <summary>
        /// Executes a transactional operation with automatic commit/rollback.
        /// If the operation fails, the transaction is rolled back and orphaned resources can be cleaned up.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The async operation to execute within the transaction.</param>
        /// <param name="onRollback">Optional cleanup action to execute on rollback (e.g., delete orphaned cloud resources).</param>
        /// <returns>The result of the operation.</returns>
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation, 
            Func<Task>? onRollback = null);
    }
}
