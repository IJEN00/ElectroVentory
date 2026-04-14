using InventoryApp.ViewModels;

namespace InventoryApp.Services
{
    public interface IReportService
    {
        Task<List<LowStockRow>> GetLowStockReportAsync(string filter);
        Task<List<ConsumptionRow>> GetConsumptionReportAsync(int days, bool projectsOnly);
        Task<List<InventoryApp.Models.Project>> GetProjectReportAsync();
    }
}
