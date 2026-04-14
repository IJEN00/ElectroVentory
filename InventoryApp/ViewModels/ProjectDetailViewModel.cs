using InventoryApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventoryApp.ViewModels
{
    public class ProjectDetailViewModel
    {
        public Project Project { get; set; } = null!;

        [Display(Name = "Součástka")]
        public int? SelectedComponentId { get; set; }

        [Display(Name = "Název (pokud není skladem)")]
        public string? CustomName { get; set; }

        [Display(Name = "Počet ks")]
        [Range(1, int.MaxValue, ErrorMessage = "Počet musí být > 0")]
        public int QuantityRequired { get; set; } = 1;

        public List<SelectListItem> AvailableComponents { get; set; } = new();

        public bool IsLocked => Project.ConsumedAt != null;
        public bool OffersSearched { get; set; } 

        public int TotalItems => Project.Items?.Count ?? 0;
        public int TotalRequired => Project.Items?.Sum(i => i.QuantityRequired) ?? 0;
        public int TotalFromStock => Project.Items?.Sum(i => i.QuantityFromStock) ?? 0;
        public int TotalToBuy => Project.Items
            .Where(i => !i.IsFulfilled) 
            .Sum(i => i.QuantityToBuy);

        public int Progress
        {
            get
            {
                if (TotalRequired == 0) return 0;

                var totalHave = Project.Items.Sum(i =>
                    i.Type == ProjectItemType.Standard
                        ? i.QuantityFromStock
                        : (i.IsFulfilled ? i.QuantityRequired : 0)
                );

                return (int)((double)totalHave / TotalRequired * 100);
            }
        }

        public string ProgressColor => Progress >= 100 ? "bg-success" : (Progress > 50 ? "bg-primary" : "bg-warning");

        public bool CanConsumeStock
        {
            get
            {
                if (Project == null || Project.Items == null || !Project.Items.Any())
                    return false;

                return Project.Items
                    .Where(i => i.Type == ProjectItemType.Standard)
                    .All(i => i.Component != null && i.Component.Quantity >= i.QuantityRequired);
            }
        }
    }
}
