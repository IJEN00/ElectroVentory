using InventoryApp.Models;
using InventoryApp.Services;
using InventoryApp.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IComponentService _components;
        private readonly IProjectService _projects; 

        public HomeController(IComponentService components, IProjectService projects)
        {
            _components = components;
            _projects = projects;
        }

        public async Task<IActionResult> Index()
        {
            var low = await _components.GetLowStockAsync();
            var stats = await _components.GetConsumptionStatsAsync(30);

            var active = await _projects.GetDashboardActiveProjectsAsync(6);

            var model = new HomeViewModel
            {
                TotalComponents = await _components.GetTotalComponentsAsync(),
                TotalQuantity = await _components.GetTotalQuantityAsync(),
                LowStockCount = low.Count,
                LowStock = low.Take(10).ToList(),

                ConsumptionLabels = stats.Labels,
                ConsumptionValues = stats.Values,

                ActiveProjects = active 
            };

            return View(model);
        }

        public IActionResult Error() => View();
    }
}