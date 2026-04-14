using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApp.Models
{
    public class Component
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = null!;

        [StringLength(200)]
        public string? Manufacturer { get; set; }

        [StringLength(100)]
        public string? ManufacturerPartNumber { get; set; }

        [StringLength(50)]
        public string? Package { get; set; }

        public int Quantity { get; set; }

        [Display(Name = "Umístění")]
        public int? LocationId { get; set; }

        [ForeignKey("LocationId")]
        public Location? Location { get; set; }

        public ICollection<Document> Documents { get; set; } = new List<Document>();

        public ICollection<ProjectItem> ProjectItems { get; set; } = new List<ProjectItem>();

        public int? ReorderPoint { get; set; } = 5;

        [NotMapped]
        public bool IsLowStock => Quantity < (ReorderPoint ?? 5);

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
