using System.ComponentModel.DataAnnotations;

namespace InventoryApp.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Regál")]
        public required string Rack { get; set; }

        [Display(Name = "Šuplík")]
        public string? Drawer { get; set; }

        [Display(Name = "Krabička")]
        public string? Box { get; set; }
        public ICollection<Component>? Components { get; set; }
        public string DisplayName => $"{Rack}{(Drawer != null ? "-" + Drawer : "")}{(Box != null ? "-" + Box : "")}";
    }
}
