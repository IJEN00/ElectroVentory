using InventoryApp.Models;
using InventoryApp.Services;
using InventoryApp.Services.Suppliers;
using InventoryApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryApp.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly IProjectService _svc;
        private readonly IComponentService _componentSvc; 

        public ProjectsController(IProjectService svc, IComponentService componentSvc)
        {
            _svc = svc;
            _componentSvc = componentSvc;
        }

        // GET: Projects
        public async Task<IActionResult> Index()
        {
            var list = await _svc.GetAllAsync();
            return View(list);
        }

        // GET: Projects/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var project = await _svc.GetDetailsAsync(id.Value);
            if (project == null) return NotFound();

            var components = await _componentSvc.GetAllAsync();
            var componentItems = components
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} (Skladem: {c.Quantity} ks)" 
                })
                .ToList();

            componentItems.Insert(0, new SelectListItem { Value = "", Text = "-- vyber součástku --" });

            var vm = new ProjectDetailViewModel
            {
                Project = project,
                AvailableComponents = componentItems,
                QuantityRequired = 1,
                OffersSearched = TempData["OffersSearched"] as bool? ?? false
            };

            ViewBag.Error = TempData["Error"];
            return View(vm);
        }

        // GET: Projects/Create
        public IActionResult Create() => View();

        // POST: Projects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Project project)
        {
            if (ModelState.IsValid)
            {
                await _svc.CreateAsync(project);
                TempData["ToastSuccess"] = $"Projekt „{project.Name}“ byl vytvořen.";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            return View(project);
        }

        // GET: Projects/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var project = await _svc.GetByIdAsync(id.Value);
            if (project == null) return NotFound();

            if (project.ConsumedAt != null)
            {
                TempData["ToastError"] = "Uzamčený projekt již nelze upravovat.";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }

            return View(project);
        }

        // POST: Projects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,CreatedAt,Status,EstimatedHours,RealHours,OrderedAt,ReceivedAt")] Project project)
        {
            if (id != project.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await _svc.UpdateAsync(project);
                    return RedirectToAction(nameof(Details), new { id = project.Id });
                }
                catch (InvalidOperationException ex)
                {
                    TempData["ToastError"] = ex.Message;
                    return RedirectToAction(nameof(Details), new { id = project.Id });
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }
            }
            return View(project);
        }

        // GET: Projects/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var project = await _svc.GetByIdAsync(id.Value);
            if (project == null) return NotFound();

            if (project.ConsumedAt != null)
            {
                TempData["ToastError"] = "Uzamčený projekt nelze smazat.";
                return RedirectToAction(nameof(Index));
            }

            return View(project);
        }

        // POST: Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _svc.DeleteAsync(id);
                TempData["ToastSuccess"] = "Projekt smazán.";
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int projectId, int? selectedComponentId, string? customName, int quantityRequired, ProjectItemType type)
        {
            try
            {
                await _svc.AddItemAsync(projectId, selectedComponentId, customName, quantityRequired, type);
            }
            catch (ArgumentException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id, int projectId) 
        {
            try
            {
                await _svc.DeleteItemAsync(id);
            }
            catch (Exception)
            {
            }
            return RedirectToAction(nameof(Details), new { id = projectId });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindOffers(int projectId)
        {
            await _svc.FindOffersAsync(projectId);
            TempData["OffersSearched"] = true;
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectOffer(int offerId, int projectId)
        {
            await _svc.SelectOfferAsync(offerId);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSelectCheapest(int projectId)
        {
            await _svc.AutoSelectCheapestAsync(projectId);
            TempData["ToastSuccess"] = "Vybrány nejlevnější nabídky.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConsumeFromStock(int id)
        {
            try
            {
                await _svc.ConsumeStockAsync(id);
                TempData["ToastSuccess"] = "Součástky byly úspěšně vyskladněny.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ToastError"] = ex.Message; 
            }
            catch (Exception)
            {
                TempData["ToastError"] = "Chyba při vyskladnění.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ExportOrderCsv(int projectId, string? supplierName = null)
        {
            try
            {
                var bytes = await _svc.GenerateOrderCsvAsync(projectId, supplierName);

                string fileName = string.IsNullOrEmpty(supplierName)
                    ? $"Objednavka_Komplet_{projectId}.csv"
                    : $"Objednavka_{supplierName.Replace(" ", "_")}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFiles(int id, List<IFormFile> files)
        {
            try
            {
                foreach (var file in files)
                {
                    await _svc.UploadFileAsync(id, file);
                }
                TempData["ToastSuccess"] = "Soubory nahrány.";
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = "Chyba nahrávání: " + ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFile(int fileId, int projectId)
        {
            await _svc.DeleteFileAsync(fileId);
            TempData["ToastSuccess"] = "Soubor smazán.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFulfillment(int itemId, int projectId)
        {
            await _svc.ToggleItemFulfillmentAsync(itemId);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicate(int id)
        {
            var newId = await _svc.DuplicateProjectAsync(id);

            if (newId > 0)
            {
                TempData["ToastSuccess"] = "Projekt byl úspěšně zkopírován.";
                return RedirectToAction(nameof(Edit), new { id = newId });
            }

            TempData["ToastError"] = "Kopírování se nezdařilo.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ExportSupplierPdf(int projectId, string supplierName)
        {
            try
            {
                var bytes = await _svc.GenerateSupplierPdfAsync(projectId, supplierName);
                return File(bytes, "application/pdf", $"Objednavka_{supplierName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = "Chyba při generování PDF: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
        }

        // TAJNÁ TESTOVACÍ STRÁNKA PRO KOMPLETNÍ MĚŘENÍ API
        [HttpGet("Projects/ZmeritApi")]
        public async Task<IActionResult> ZmeritApiOdezvu(
            [FromServices] ISupplierClient _apiClient)
        {
            var sw = new System.Diagnostics.Stopwatch();
            var vysledky = new System.Text.StringBuilder();

            vysledky.AppendLine("=== KOMPLETNÍ MĚŘENÍ ODEZVY API PRO BAKALÁŘKU ===");

            // Pomocná lokální funkce pro vytvoření položky
            ProjectItem VytvorTestovaciPolozku(string nazevDilu)
            {
                return new ProjectItem
                {
                    CustomName = nazevDilu,
                    QuantityRequired = 1
                };
            }

            // Pomocná lokální funkce pro spuštění jedné sady měření
            async Task ProvedTest(int pocet, string nazevTestu)
            {
                vysledky.AppendLine($"\n{nazevTestu}: {pocet} položek paralelně");
                var tasks = new List<Task<List<SupplierOffer>>>();

                sw.Restart();
                for (int i = 0; i < pocet; i++)
                {
                    // Vygenerujeme názvy typu "TEST-PART-1", "TEST-PART-2"...
                    var item = VytvorTestovaciPolozku($"TEST-PART-{i}");
                    tasks.Add(_apiClient.SearchAsync(item));
                }

                // Počkáme na dokončení všech dotazů
                await Task.WhenAll(tasks);
                sw.Stop();

                vysledky.AppendLine($"Celkový čas pro {pocet} položek: {sw.ElapsedMilliseconds} ms");
                vysledky.AppendLine($"Průměrný čas na 1 položku: {sw.ElapsedMilliseconds / pocet} ms");
            }

            // --- SPUŠTĚNÍ VŠECH TESTŮ ---

            // TEST 1: 1 položka
            await ProvedTest(1, "TEST 1");
            await Task.Delay(1500); // Pauza na vydechnutí API

            // TEST 2: 5 položek
            await ProvedTest(5, "TEST 2");
            await Task.Delay(1500);

            // TEST 3: 10 položek
            await ProvedTest(10, "TEST 3");
            await Task.Delay(2000); // Tady dáme raději 2 vteřiny

            // TEST 4: 50 položek
            await ProvedTest(50, "TEST 4");

            return Content(vysledky.ToString(), "text/plain", System.Text.Encoding.UTF8);
        }
    }
}