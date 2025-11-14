using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Pit2Hi022052.ViewModels;

namespace Pit2Hi022052.Services
{
    public class BalanceSheetService : IBalanceSheetService
    {
        private readonly ApplicationDbContext _dbContext;

        public BalanceSheetService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BalanceSheetSummary> GetLatestSummaryAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BalanceSheetSummary.Empty;
            }

            var query = _dbContext.BalanceSheetEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId && !e.IsDeleted);

            var latestDate = await query.MaxAsync(e => (DateTime?)e.AsOfDate, cancellationToken);
            if (latestDate is null)
            {
                return BalanceSheetSummary.Empty;
            }

            var snapshotDate = latestDate.Value.Date;
            var snapshot = await query
                .Where(e => e.AsOfDate == snapshotDate)
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            var assets = snapshot.Where(e => e.Type == BalanceSheetEntryType.Asset).ToList();
            var liabilities = snapshot.Where(e => e.Type == BalanceSheetEntryType.Liability).ToList();

            return new BalanceSheetSummary
            {
                TotalAssets = assets.Sum(e => e.Amount),
                TotalLiabilities = liabilities.Sum(e => e.Amount),
                Assets = assets,
                Liabilities = liabilities,
                AsOfDate = snapshotDate
            };
        }

        public async Task AddEntryAsync(string userId, BalanceSheetEntry entry, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("UserId is required.", nameof(userId));
            }

            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.Amount < 0)
            {
                throw new ArgumentException("Amount must be zero or greater.", nameof(entry));
            }

            entry.UserId = userId;
            entry.AsOfDate = entry.AsOfDate == default ? DateTime.UtcNow.Date : entry.AsOfDate.Date;

            _dbContext.BalanceSheetEntries.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteEntryAsync(string userId, int entryId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("UserId is required.", nameof(userId));
            }

            var entry = await _dbContext.BalanceSheetEntries
                .Where(e => e.UserId == userId && e.Id == entryId && !e.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry is null)
            {
                return;
            }

            entry.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
