using InventoryApp.Models;
using InventoryApp.Services;
using InventoryApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace InventoryApp.Controllers
{
    public class ComponentsController : Controller
    {
        private readonly IComponentService _svc;
        private readonly ILocationService _locationSvc;
        private readonly IDocumentService _docSvc;
        private readonly IWebHostEnvironment _env;
        private readonly SupplierAggregatorService _aggregator;

        public ComponentsController(IComponentService svc, ILocationService locationSvc, IDocumentService doc, IWebHostEnvironment env, SupplierAggregatorService aggregator)
        {
            _svc = svc;
            _locationSvc = locationSvc;
            _env = env;
            _docSvc = doc;
            _aggregator = aggregator;
        }

        // --- ÚPRAVA: Přidán parametr showInactive ---
        public async Task<IActionResult> Index(bool showInactive = false)
        {
            var list = await _svc.GetAllAsync();

            if (showInactive)
            {
                // Ukáže POUZE archivované
                list = list.Where(c => !c.IsActive).ToList();
            }
            else
            {
                // Ukáže POUZE aktivní
                list = list.Where(c => c.IsActive).ToList();
            }

            ViewBag.ShowingInactive = showInactive;
            return View(list);
        }

        // --- ÚPRAVA: Přidán parametr showInactive ---
        [HttpGet]
        public async Task<IActionResult> Filter(string? rack, string? drawer, string? box, string? search, bool showInactive = false)
        {
            var list = await _svc.FilterComponentsAsync(rack, drawer, box, search);

            if (!showInactive)
            {
                list = list.Where(c => c.IsActive).ToList();
            }

            return PartialView("_ComponentListPartial", list);
        }

        public async Task<IActionResult> Create()
        {
            var locations = await _locationSvc.GetAllAsync();

            var vm = new ComponentViewModel
            {
                LocationOptions = new SelectList(locations, "Id", "DisplayName"),
                ReorderPoint = 5
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComponentViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var locations = await _locationSvc.GetAllAsync();
                vm.LocationOptions = new SelectList(locations, "Id", "DisplayName", vm.LocationId);
                return View(vm);
            }

            var component = new Models.Component
            {
                Name = vm.Name,
                Manufacturer = vm.Manufacturer,
                ManufacturerPartNumber = vm.ManufacturerPartNumber,
                Package = vm.Package,
                Quantity = vm.Quantity,
                ReorderPoint = vm.ReorderPoint,
                LocationId = vm.LocationId,
                IsActive = true 
            };

            if (vm.ImageUpload != null && vm.ImageUpload.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "components");
                Directory.CreateDirectory(uploadsFolder); 
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(vm.ImageUpload.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.ImageUpload.CopyToAsync(fileStream);
                }
                component.ImagePath = "/uploads/components/" + uniqueFileName;
            }

            await _svc.AddAsync(component);
            await _docSvc.UploadFilesAsync(vm.Files, component.Id, _env.WebRootPath);

            TempData["ToastSuccess"] = $"Součástka „{component.Name}“ byla vytvořena.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var component = await _svc.GetByIdAsync(id);
            if (component == null) return NotFound();

            var locations = await _locationSvc.GetAllAsync();

            var vm = new ComponentViewModel
            {
                Id = component.Id,
                Name = component.Name,
                Manufacturer = component.Manufacturer,
                ManufacturerPartNumber = component.ManufacturerPartNumber,
                Package = component.Package,
                Quantity = component.Quantity,
                ReorderPoint = component.ReorderPoint,
                LocationId = component.LocationId,
                CurrentImagePath = component.ImagePath,
                ExistingDocuments = component.Documents ?? new List<Document>(),
                LocationOptions = new SelectList(locations, "Id", "DisplayName", component.LocationId)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ComponentViewModel vm)
        {
            if (id != vm.Id) return BadRequest();

            if (!ModelState.IsValid)
            {
                var locations = await _locationSvc.GetAllAsync();
                vm.LocationOptions = new SelectList(locations, "Id", "DisplayName", vm.LocationId);

                var original = await _svc.GetByIdAsync(id);
                vm.ExistingDocuments = original?.Documents ?? new List<Document>();
                return View(vm);
            }

            var componentToUpdate = await _svc.GetByIdAsync(id);
            if (componentToUpdate == null) return NotFound();

            componentToUpdate.Name = vm.Name;
            componentToUpdate.Manufacturer = vm.Manufacturer;
            componentToUpdate.ManufacturerPartNumber = vm.ManufacturerPartNumber;
            componentToUpdate.Package = vm.Package;
            componentToUpdate.ReorderPoint = vm.ReorderPoint;
            componentToUpdate.LocationId = vm.LocationId;

            if (vm.ImageUpload != null && vm.ImageUpload.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "components");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(vm.ImageUpload.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.ImageUpload.CopyToAsync(fileStream);
                }
                componentToUpdate.ImagePath = "/uploads/components/" + uniqueFileName;
            }

            await _svc.UpdateAsync(componentToUpdate);
            await _docSvc.UploadFilesAsync(vm.Files, componentToUpdate.Id, _env.WebRootPath);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var component = await _svc.GetByIdAsync(id);
            if (component == null) return NotFound();

            var history = await _svc.GetTransactionHistoryAsync(id);

            var vm = new ComponentDetailViewModel
            {
                Id = component.Id,
                Name = component.Name,
                Manufacturer = component.Manufacturer,
                ManufacturerPartNumber = component.ManufacturerPartNumber,
                Package = component.Package,
                Quantity = component.Quantity,
                ReorderPoint = component.ReorderPoint ?? 5,
                Location = component.Location,
                Documents = component.Documents.ToList(),
                ImagePath = component.ImagePath,
                IsActive = component.IsActive,
                History = history
            };

            return View(vm);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var model = await _svc.GetByIdAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            await _docSvc.DeleteAsync(id);
            return Ok();
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var component = await _svc.GetByIdAsync(id);
                if (component == null) return NotFound();

                if (component.IsActive)
                {
                    component.IsActive = false;
                    await _svc.UpdateAsync(component);
                    TempData["ToastSuccess"] = $"Součástka „{component.Name}“ byla archivována. Její historie v projektech zůstala zachována.";
                }
                else
                {
                    await _svc.DeleteAsync(id);
                    TempData["ToastSuccess"] = $"Součástka „{component.Name}“ byla trvale odstraněna.";
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["ToastError"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = "Chyba při mazání součástky: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStock(int id, int amount, string? supplier, string? note)
        {
            try
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(supplier)) parts.Add(supplier);
                if (!string.IsNullOrWhiteSpace(note)) parts.Add(note);

                string finalNote = string.Join(" - ", parts);

                await _svc.AddStockAsync(id, amount, string.IsNullOrEmpty(finalNote) ? null : finalNote);

                TempData["ToastSuccess"] = "Součástka byla úspěšně naskladněna.";
            }
            catch (ArgumentException ex)
            {
                TempData["ToastError"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = $"Chyba při naskladnění: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UseStock(int id, int amount, string? note)
        {
            try
            {
                await _svc.UseStockAsync(id, amount, note);
                TempData["ToastSuccess"] = "Součástka byla úspěšně vyskladněna.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ToastError"] = ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["ToastError"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = $"Chyba při vyskladnění: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> SearchOffers(int id)
        {
            var component = await _svc.GetByIdAsync(id);
            if (component == null) return NotFound();

            var offers = await _aggregator.SearchForComponentAsync(component);

            return PartialView("_SupplierOffersPartial", offers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicate(int id)
        {
            var newId = await _svc.DuplicateAsync(id);

            if (newId > 0)
            {
                TempData["ToastSuccess"] = "Součástka byla úspěšně zkopírována.";
                return RedirectToAction(nameof(Edit), new { id = newId });
            }

            TempData["ToastError"] = "Kopírování se nezdařilo (součástka nenalezena).";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public IActionResult DownloadCsvTemplate()
        {
            var builder = new System.Text.StringBuilder();

            builder.AppendLine("Nazev;Vyrobce;MPN;Skladem;Minimum");

            builder.AppendLine("Rezistor 10k;Vishay;CRCW080510K0FKEA;100;20");
            builder.AppendLine("Mikrospínač;Omron;B3F-1000;50;10");

            var encoding = new System.Text.UTF8Encoding(true); 
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(builder.ToString())).ToArray();

            return File(bytes, "text/csv", "Sablona_Soucastky.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ToastError"] = "Nebyl vybrán žádný soubor k importu.";
                return RedirectToAction(nameof(Index));
            }

            using var stream = file.OpenReadStream();
            var result = await _svc.ImportFromCsvAsync(stream);

            if (result.errors.Any())
            {
                TempData["ToastWarning"] = $"Importováno: {result.importedCount} položek. Zbytek selhal. Ukázka chyb: {string.Join(" | ", result.errors.Take(3))}";
            }
            else
            {
                TempData["ToastSuccess"] = $"Úspěšně importováno všech {result.importedCount} součástek.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}