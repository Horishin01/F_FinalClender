using Pit2Hi022052.Models;
using Pit2Hi022052.ViewModels;

namespace Pit2Hi022052.Services
{
    public interface IBalanceSheetService
    {
        Task<BalanceSheetSummary> GetLatestSummaryAsync(string userId, CancellationToken cancellationToken = default);
        Task AddEntryAsync(string userId, BalanceSheetEntry entry, CancellationToken cancellationToken = default);
        Task DeleteEntryAsync(string userId, int entryId, CancellationToken cancellationToken = default);
    }
}
