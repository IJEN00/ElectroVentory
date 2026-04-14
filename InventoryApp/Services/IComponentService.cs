using InventoryApp.Models;

namespace InventoryApp.Services
{
    public interface IComponentService
    {
        Task<List<Component>> GetAllAsync();
        Task<Component?> GetByIdAsync(int id);
        Task AddAsync(Component component);
        Task UpdateAsync(Component component);
        Task DeleteAsync(int id);
        Task<List<Component>> GetLowStockAsync();
        Task<int> GetTotalComponentsAsync();
        Task<int> GetTotalQuantityAsync();
        Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int componentId, int count = 10);
        Task<List<Component>> FilterComponentsAsync(string? rack, string? drawer, string? box, string? search);
        Task<(List<string> Labels, List<int> Values)> GetConsumptionStatsAsync(int days);
        Task AddStockAsync(int id, int amount, string? note);
        Task UseStockAsync(int id, int amount, string? note);
        Task<int> DuplicateAsync(int id);
        Task<(int importedCount, List<string> errors)> ImportFromCsvAsync(Stream fileStream);
    }
}
