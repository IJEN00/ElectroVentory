using InventoryApp.Services;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryApp.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportService _svc;

        public ReportsController(IReportService svc)
        {
            _svc = svc;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(LowStock));
        }

        public async Task<IActionResult> LowStock(string filter = "all")
        {
            var rows = await _svc.GetLowStockReportAsync(filter);

            ViewBag.Filter = filter;
            return View(rows);
        }

        public async Task<IActionResult> Consumption(int days = 30, bool projectsOnly = false)
        {
            var rows = await _svc.GetConsumptionReportAsync(days, projectsOnly);

            ViewBag.Days = days;
            ViewBag.ProjectsOnly = projectsOnly;
            return View(rows);
        }

        public async Task<IActionResult> Projects()
        {
            var rows = await _svc.GetProjectReportAsync();
            return View(rows);
        }
    }
}