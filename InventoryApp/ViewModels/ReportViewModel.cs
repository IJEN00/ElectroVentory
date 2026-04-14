namespace InventoryApp.ViewModels
{
    public class LowStockRow
    {
        public int ComponentId { get; set; }
        public string Name { get; set; } = "";
        public string? ManufacturerPartNumber { get; set; }
        public int Quantity { get; set; }
        public int ReorderPoint { get; set; }
        public int ToBuy { get; set; }
        public string LocationDisplay { get; set; } = "–";
        public string? LastSupplier { get; set; }
    }

    public class ConsumptionRow
    {
        public int ComponentId { get; set; }
        public string Name { get; set; } = "";
        public string? ManufacturerPartNumber { get; set; }
        public int Used { get; set; }
        public int UsesCount { get; set; }
        public int Quantity { get; set; }
        public int ReorderPoint { get; set; }
        public int ToBuy { get; set; }
        public int UsedFromProjects { get; set; }
        public int UsedManual { get; set; }

        public string SourceLabel =>
            UsedFromProjects > 0 && UsedManual > 0 ? "Mix" :
            UsedFromProjects > 0 ? "Projekty" :
            "Ruční";
    }
}
