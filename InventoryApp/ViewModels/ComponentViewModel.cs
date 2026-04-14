using Microsoft.AspNetCore.Mvc.Rendering;
using InventoryApp.Models;
using System.ComponentModel.DataAnnotations;

namespace InventoryApp.ViewModels
{
    public class ComponentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Název je povinný")]
        [StringLength(200, ErrorMessage = "Název může mít maximálně 200 znaků")] 
        [Display(Name = "Název součástky")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)] 
        [Display(Name = "Výrobce")]
        public string? Manufacturer { get; set; }

        [StringLength(100)] 
        [Display(Name = "MPN (Kód výrobce)")]
        public string? ManufacturerPartNumber { get; set; }

        [StringLength(50)] 
        [Display(Name = "Pouzdro (Package)")]
        public string? Package { get; set; }

        [Display(Name = "Množství")]
        [Range(0, int.MaxValue, ErrorMessage = "Množství nesmí být záporné")]
        public int Quantity { get; set; }

        [Display(Name = "Minimální limit")]
        public int? ReorderPoint { get; set; } = 5; 

        [Display(Name = "Umístění")]
        public int? LocationId { get; set; }

        public SelectList? LocationOptions { get; set; }

        [Display(Name = "Přílohy")]
        public List<IFormFile> Files { get; set; } = new();

        public ICollection<Document> ExistingDocuments { get; set; } = new List<Document>();

        public IFormFile? ImageUpload { get; set; }

        public string? CurrentImagePath { get; set; }
    }
}
