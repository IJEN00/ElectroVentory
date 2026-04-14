using InventoryApp.Models;

namespace InventoryApp.ViewModels
{
    public class ComponentDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Manufacturer { get; set; }
        public string? ManufacturerPartNumber { get; set; }
        public string? Package { get; set; }
        public int Quantity { get; set; }
        public int ReorderPoint { get; set; }

        public Location? Location { get; set; }
        public List<Document> Documents { get; set; } = new();
        public List<InventoryTransaction> History { get; set; } = new();

        public bool IsOutOfStock => Quantity == 0;
        public bool IsLowStock => !IsOutOfStock && Quantity < ReorderPoint;

        public string StatusColor => IsOutOfStock ? "danger" : (IsLowStock ? "warning" : "success");
        public string StatusText => IsOutOfStock ? "Není skladem" : (IsLowStock ? "Dochází" : "Skladem");
        public string StatusIcon => IsOutOfStock ? "bi-x-circle" : (IsLowStock ? "bi-exclamation-circle" : "bi-check-circle");

        public int ToBuy => Math.Max(ReorderPoint - Quantity, 0);
        public bool IsActive { get; set; }
        public string? ImagePath { get; set; }
    }
}
