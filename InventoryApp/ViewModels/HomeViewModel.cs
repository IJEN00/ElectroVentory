using InventoryApp.Models;

namespace InventoryApp.ViewModels
{
    public class HomeViewModel
    {
        public int TotalComponents { get; set; }
        public int TotalQuantity { get; set; }
        public int LowStockCount { get; set; }
        public List<Component> LowStock { get; set; } = new();

        public List<string> ConsumptionLabels { get; set; } = new();
        public List<int> ConsumptionValues { get; set; } = new();
        public List<Project> ActiveProjects { get; set; } = new();
    }
}
