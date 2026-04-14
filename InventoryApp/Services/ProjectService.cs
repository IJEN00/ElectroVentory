using System.Text;
using InventoryApp.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace InventoryApp.Services
{
    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _context;
        private readonly IProjectPlanningService _planningService;
        private readonly SupplierAggregatorService _aggregator;
        private readonly IWebHostEnvironment _env;

        public ProjectService(AppDbContext context, IProjectPlanningService planningService, SupplierAggregatorService aggregator, IWebHostEnvironment env)
        {
            _context = context;
            _planningService = planningService;
            _aggregator = aggregator;
            _env = env;
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _context.Projects
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Project>> GetDashboardActiveProjectsAsync(int limit = 6)
        {
            return await _context.Projects
                .Include(p => p.Items) 
                .Where(p => p.Status != ProjectStatus.Completed && p.ConsumedAt == null) 
                .OrderByDescending(p => p.CreatedAt) 
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects.FindAsync(id);
        }

        public async Task<Project?> GetDetailsAsync(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Items)
                    .ThenInclude(i => i.Component)
                        .ThenInclude(c => c.Location) 
                .Include(p => p.Items)
                    .ThenInclude(i => i.SupplierOffers)
                        .ThenInclude(o => o.Supplier)
                .Include(p => p.Documents) 
                .AsSplitQuery() 
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project != null && project.ConsumedAt == null)
            {
                _planningService.RecalculateInMemory(project);
            }

            return project;
        }

        public async Task CreateAsync(Project project)
        {
            project.CreatedAt = DateTime.UtcNow;
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Project project)
        {
            var existingProject = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == project.Id);
            if (existingProject != null && existingProject.ConsumedAt != null)
            {
                throw new InvalidOperationException("Uzamčený projekt již nelze upravovat.");
            }

            try
            {
                _context.Projects.Update(project);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(project.Id)) throw new KeyNotFoundException("Projekt neexistuje.");
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Items)
                    .ThenInclude(i => i.SupplierOffers)
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) throw new KeyNotFoundException("Projekt nenalezen.");

            if (project.ConsumedAt != null)
                throw new InvalidOperationException("Uzamčený (vyskladněný) projekt nelze smazat.");

            if (project.Documents != null && project.Documents.Any())
            {
                foreach (var doc in project.Documents)
                {
                    var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                _context.Documents.RemoveRange(project.Documents);
            }

            if (project.Items != null && project.Items.Any())
            {
                foreach (var item in project.Items)
                {
                    if (item.SupplierOffers != null && item.SupplierOffers.Any())
                    {
                        _context.SupplierOffers.RemoveRange(item.SupplierOffers);
                    }
                }
                _context.ProjectItems.RemoveRange(project.Items);
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }

        public bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.Id == id);
        }

        public async Task AddItemAsync(int projectId, int? componentId, string? customName, int quantity, ProjectItemType type = ProjectItemType.Standard)
        {
            if (quantity <= 0) throw new ArgumentException("Počet kusů musí být > 0");

            var project = await _context.Projects.FindAsync(projectId);
            if (project != null && project.ConsumedAt != null)
                throw new InvalidOperationException("Do uzamčeného projektu nelze přidávat položky.");

            var item = new ProjectItem
            {
                ProjectId = projectId,
                ComponentId = componentId,
                CustomName = componentId.HasValue ? null : customName,
                QuantityRequired = quantity,
                QuantityToBuy = quantity,
                Type = type
            };

            _context.ProjectItems.Add(item);
            await _context.SaveChangesAsync();

            await _planningService.RecalculateAndSaveAsync(projectId);
        }

        public async Task DeleteItemAsync(int itemId)
        {
            var item = await _context.ProjectItems
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) throw new KeyNotFoundException("Položka nenalezena.");

            if (item.Project != null && item.Project.ConsumedAt != null)
                throw new InvalidOperationException("Z uzamčeného projektu nelze mazat položky.");

            int projectId = item.ProjectId;
            _context.ProjectItems.Remove(item);
            await _context.SaveChangesAsync();

            await _planningService.RecalculateAndSaveAsync(projectId);
        }

        public async Task FindOffersAsync(int projectId)
        {
            var exists = await _context.Projects.AnyAsync(p => p.Id == projectId);
            if (!exists) throw new KeyNotFoundException("Projekt nenalezen.");

            await _planningService.RecalculateAndSaveAsync(projectId);

            await _aggregator.GenerateOffersForProjectAsync(projectId);
        }

        public async Task SelectOfferAsync(int offerId)
        {
            var offer = await _context.SupplierOffers
                .Include(o => o.ProjectItem) 
                .ThenInclude(i => i.SupplierOffers) 
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer != null)
            {
                foreach (var otherOffer in offer.ProjectItem.SupplierOffers)
                {
                    otherOffer.IsSelected = false;
                }

                offer.IsSelected = true;

                await _context.SaveChangesAsync();
            }
        }

        public async Task ConsumeStockAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Items)
                .ThenInclude(i => i.Component)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) throw new KeyNotFoundException("Projekt nenalezen.");
            if (project.ConsumedAt != null) throw new InvalidOperationException("Projekt již byl vyskladněn.");

            bool hasItems = project.Items != null && project.Items.Any();

            if (!hasItems)
            {
                throw new InvalidOperationException("Nelze vyskladnit prázdný projekt. Přidejte nejprve položky do kusovníku.");
            }

            bool isMaterialReady = project.Items == null || project.Items
                .Where(i => i.Type == ProjectItemType.Standard)
                .All(i => i.Component != null && i.Component.Quantity >= i.QuantityRequired);

            if (!isMaterialReady)
            {
                throw new InvalidOperationException("Nelze vyskladnit. Některé součástky chybí nebo jich není dostatek.");
            }

            _planningService.RecalculateInMemory(project);

            foreach (var item in project.Items.Where(i => i.ComponentId != null && i.QuantityFromStock > 0))
            {
                if (item.Component == null) continue;
                if (item.QuantityFromStock > item.Component.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Nelze odečíst {item.QuantityFromStock} ks z '{item.Component.Name}'. Skladem jen {item.Component.Quantity}.");
                }
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in project.Items.Where(i => i.ComponentId != null && i.QuantityFromStock > 0))
                {
                    if (item.Component == null) continue;

                    item.Component.Quantity -= item.QuantityFromStock; 

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ComponentId = item.Component.Id,
                        DeltaQuantity = -item.QuantityFromStock,
                        Type = InventoryTransactionType.Use,
                        ProjectId = project.Id,
                        Note = $"Projekt: {project.Name}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                project.ConsumedAt = DateTime.UtcNow;
                project.Status = ProjectStatus.Completed;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task AutoSelectCheapestAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Items)
                .ThenInclude(i => i.SupplierOffers)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return;

            foreach (var item in project.Items)
            {
                if (item.QuantityToBuy <= 0 || item.SupplierOffers == null || !item.SupplierOffers.Any())
                    continue;

                var cheapest = item.SupplierOffers.OrderBy(o => o.UnitPrice).FirstOrDefault();
                if (cheapest == null) continue;

                foreach (var offer in item.SupplierOffers)
                {
                    offer.IsSelected = (offer.Id == cheapest.Id);
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<byte[]> GenerateOrderCsvAsync(int projectId, string? supplierName = null)
        {
            var project = await GetDetailsAsync(projectId);
            if (project == null) throw new KeyNotFoundException("Projekt nenalezen.");

            var selectedOffers = project.Items
                .Where(i => i.SupplierOffers != null && i.SupplierOffers.Any(o => o.IsSelected))
                .SelectMany(i => i.SupplierOffers
                    .Where(o => o.IsSelected && (supplierName == null || o.Supplier?.Name == supplierName))
                    .Select(o => new { Item = i, Offer = o }))
                .ToList();

            if (!selectedOffers.Any()) throw new InvalidOperationException("Žádné položky k objednání.");

            var sb = new StringBuilder();
            sb.AppendLine("Dodavatel;Součástka;Počet;Cena/ks;Měna;Celkem");

            foreach (var x in selectedOffers)
            {
                var partName = x.Item.Component?.Name ?? x.Item.CustomName ?? "Unknown";
                var total = x.Item.QuantityToBuy * x.Offer.UnitPrice;

                string Escape(string s) => s.Contains(';') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

                sb.AppendLine($"{Escape(x.Offer.Supplier?.Name ?? "")};{Escape(partName)};{x.Item.QuantityToBuy};{x.Offer.UnitPrice:F2};{x.Offer.Currency};{total:F2}");
            }

            var content = sb.ToString();

            var encoding = new UTF8Encoding(true);
            var preamble = encoding.GetPreamble(); 
            var data = encoding.GetBytes(content); 

            var complete = new byte[preamble.Length + data.Length];
            Buffer.BlockCopy(preamble, 0, complete, 0, preamble.Length);
            Buffer.BlockCopy(data, 0, complete, preamble.Length, data.Length);

            return complete;
        }

        public async Task UploadFileAsync(int projectId, IFormFile file)
        {
            if (file == null || file.Length == 0) return;

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "projects", projectId.ToString());
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new InventoryApp.Models.Document
            {
                ProjectId = projectId,
                FileName = fileName,
                FilePath = $"/uploads/projects/{projectId}/{uniqueFileName}",
                UploadedAt = DateTime.UtcNow
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteFileAsync(int fileId)
        {
            var doc = await _context.Documents.FindAsync(fileId);
            if (doc == null) return;

            var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleItemFulfillmentAsync(int itemId)
        {
            var item = await _context.ProjectItems.FindAsync(itemId);
            if (item != null)
            {
                item.IsFulfilled = !item.IsFulfilled;

                if (item.IsFulfilled)
                {
                    item.QuantityToBuy = 0;
                }
                else
                {
                    item.QuantityToBuy = Math.Max(0, item.QuantityRequired - item.QuantityFromStock);
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> DuplicateProjectAsync(int originalId)
        {
            var original = await _context.Projects
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == originalId);

            if (original == null) return 0;

            var newProject = new Project
            {
                Name = original.Name + " (Kopie)", 
                Description = original.Description,
                Status = ProjectStatus.Planning, 
                CreatedAt = DateTime.UtcNow,
                EstimatedHours = original.EstimatedHours,

                RealHours = 0,
                OrderedAt = null,
                ReceivedAt = null,
                ConsumedAt = null 
            };

            _context.Projects.Add(newProject);
            await _context.SaveChangesAsync();

            foreach (var item in original.Items)
            {
                var newItem = new ProjectItem
                {
                    ProjectId = newProject.Id,
                    ComponentId = item.ComponentId,
                    CustomName = item.CustomName,
                    Type = item.Type,
                    QuantityRequired = item.QuantityRequired,

                    QuantityFromStock = 0, 
                    QuantityToBuy = 0,     
                    IsFulfilled = false    
                };

                if (newItem.ComponentId == null)
                {
                    newItem.QuantityToBuy = newItem.QuantityRequired;
                }

                _context.ProjectItems.Add(newItem);
            }

            await _context.SaveChangesAsync();
            return newProject.Id; 
        }

        public async Task<byte[]> GenerateSupplierPdfAsync(int projectId, string supplierName)
        {
            // Nutné pro použití QuestPDF zdarma
            QuestPDF.Settings.License = LicenseType.Community;

            var project = await GetDetailsAsync(projectId);
            if (project == null) throw new KeyNotFoundException("Projekt nenalezen.");

            // Vyfiltrujeme jen položky pro tohoto konkrétního dodavatele
            var itemsForSupplier = project.Items
                .Where(i => !i.IsFulfilled && i.QuantityToBuy > 0 && i.SupplierOffers != null)
                .SelectMany(i => i.SupplierOffers
                    .Where(o => o.IsSelected && o.Supplier?.Name == supplierName)
                    .Select(o => new { Item = i, Offer = o }))
                .ToList();

            if (!itemsForSupplier.Any()) throw new InvalidOperationException("Žádné položky pro tohoto dodavatele.");

            decimal totalSum = itemsForSupplier.Sum(x => x.Item.QuantityToBuy * x.Offer.UnitPrice);
            string currency = itemsForSupplier.First().Offer.Currency;

            // Generování PDF
            var pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Hlavička
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Nákupní seznam: {supplierName}").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Projekt: {project.Name}").FontSize(14).Light();
                            col.Item().Text($"Vytvořeno: {DateTime.Now:d. M. yyyy}").FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Název dílu
                            columns.RelativeColumn(3); // Objednací číslo 
                            columns.RelativeColumn(1); // Počet ks
                            columns.RelativeColumn(2); // Cena celkem
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).PaddingBottom(5).Text("Součástka").SemiBold();
                            header.Cell().BorderBottom(1).PaddingBottom(5).Text("Objednací číslo (SKU)").SemiBold();
                            header.Cell().BorderBottom(1).PaddingBottom(5).AlignRight().Text("Ks").SemiBold();
                            header.Cell().BorderBottom(1).PaddingBottom(5).AlignRight().Text("Cena").SemiBold();
                        });

                        foreach (var line in itemsForSupplier)
                        {
                            var partName = line.Item.Component?.Name ?? line.Item.CustomName ?? "Neznámý";
                            var sku = line.Offer.SupplierPartNumber ?? "N/A";
                            var lineTotal = line.Item.QuantityToBuy * line.Offer.UnitPrice;

                            table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Text(partName);
                            table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Text(sku).FontFamily("Consolas"); 
                            table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Text(line.Item.QuantityToBuy.ToString());
                            table.Cell().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Text($"{lineTotal:N2} {currency}");
                        }

                        table.Cell().ColumnSpan(3).PaddingTop(10).AlignRight().Text("Celkem k úhradě:").SemiBold().FontSize(14);
                        table.Cell().PaddingTop(10).AlignRight().Text($"{totalSum:N2} {currency}").SemiBold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Strana ");
                        x.CurrentPageNumber();
                        x.Span(" z ");
                        x.TotalPages();
                    });
                });
            });

            return pdf.GeneratePdf();
        }
    }
}