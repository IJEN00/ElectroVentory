using InventoryApp.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace InventoryApp.Services
{
    public class ComponentService : IComponentService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ComponentService(AppDbContext db, IWebHostEnvironment env) 
        { 
            _db = db; 
            _env = env;
        }


        public async Task AddAsync(Component component)
        {
            _db.Components.Add(component);
            await _db.SaveChangesAsync();
        }


        public async Task DeleteAsync(int id)
        {
            var entity = await _db.Components
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (entity == null) return;

            bool isUsedInProjects = await _db.ProjectItems.AnyAsync(pi => pi.ComponentId == id);
            if (isUsedInProjects)
            {
                throw new InvalidOperationException("Tuto součástku nelze smazat, protože je součástí jednoho nebo více projektů. Místo toho ji prosím Archivujte.");
            }

            var transactions = await _db.InventoryTransactions.Where(t => t.ComponentId == id).ToListAsync();
            if (transactions.Any())
            {
                _db.InventoryTransactions.RemoveRange(transactions);
            }

            if (!string.IsNullOrEmpty(entity.ImagePath))
            {
                var imgPath = Path.Combine(_env.WebRootPath, entity.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(imgPath))
                {
                    System.IO.File.Delete(imgPath);
                }
            }

            if (entity.Documents != null && entity.Documents.Any())
            {
                foreach (var doc in entity.Documents)
                {
                    var docPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(docPath))
                    {
                        System.IO.File.Delete(docPath);
                    }
                }
                _db.Documents.RemoveRange(entity.Documents);
            }

            _db.Components.Remove(entity);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Component>> GetAllAsync()
        {
            return await _db.Components.Include(c => c.Documents).Include(c => c.Location).ToListAsync();
        }


        public async Task<Component?> GetByIdAsync(int id)
        {
            return await _db.Components.Include(c => c.Documents).Include(c => c.Location).FirstOrDefaultAsync(c => c.Id == id);
        }


        public async Task<List<Component>> GetLowStockAsync()
        {
            return await _db.Components
                .Include(c => c.Location)
                .Where(c => c.IsActive && c.Quantity < (c.ReorderPoint ?? 5))
                .OrderBy(c => c.Quantity)
                .ToListAsync();
        }


        public async Task UpdateAsync(Component component)
        {
            _db.Components.Update(component);
            await _db.SaveChangesAsync();
        }

        public Task<int> GetTotalComponentsAsync()
            => _db.Components.Where(c => c.IsActive).CountAsync();

        public Task<int> GetTotalQuantityAsync()
            => _db.Components.Where(c => c.IsActive).SumAsync(c => c.Quantity);

        public async Task<(List<string> Labels, List<int> Values)> GetConsumptionStatsAsync(int days)
        {
            var since = DateTime.UtcNow.AddDays(-days);

            var consumption = await _db.InventoryTransactions
                .Where(t => t.CreatedAt >= since && t.DeltaQuantity < 0)
                .GroupBy(t => t.ComponentId)
                .Select(g => new
                {
                    ComponentId = g.Key,
                    Used = -g.Sum(x => x.DeltaQuantity) 
                })
                .OrderByDescending(x => x.Used)
                .Take(7) 
                .ToListAsync();

            var componentIds = consumption.Select(x => x.ComponentId).ToList();
            var components = await _db.Components
                .Where(c => componentIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            var labels = new List<string>();
            var values = new List<int>();

            foreach (var item in consumption)
            {
                var name = components.FirstOrDefault(c => c.Id == item.ComponentId)?.Name ?? "Neznámá";
                labels.Add(name);
                values.Add(item.Used);
            }

            return (labels, values);
        }

        public async Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int componentId, int count = 10)
        {
            return await _db.InventoryTransactions
                .Include(t => t.Component) 
                .Where(t => t.ComponentId == componentId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Component>> FilterComponentsAsync(string? rack, string? drawer, string? box, string? search)
        {
            var query = _db.Components
                .Include(c => c.Location)  
                .Include(c => c.Documents) 
                .AsQueryable();

            if (!string.IsNullOrEmpty(rack))
            {
                query = query.Where(c => c.Location != null && c.Location.Rack == rack);
            }

            if (!string.IsNullOrEmpty(drawer))
            {
                query = query.Where(c => c.Location != null && c.Location.Drawer == drawer);
            }

            if (!string.IsNullOrEmpty(box))
            {
                query = query.Where(c => c.Location != null && c.Location.Box == box);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search));
            }

            return await query.ToListAsync();
        }

        public async Task AddStockAsync(int id, int amount, string? note)
        {
            if (amount <= 0) throw new ArgumentException("Množství musí být větší než 0.");

            var component = await _db.Components.FindAsync(id);
            if (component == null) throw new Exception("Součástka nenalezena.");

            component.Quantity += amount;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ComponentId = id,
                DeltaQuantity = amount,
                Type = InventoryTransactionType.Add,
                Note = note,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task UseStockAsync(int id, int amount, string? note)
        {
            if (amount <= 0) throw new ArgumentException("Množství musí být větší než 0.");

            var component = await _db.Components.FindAsync(id);
            if (component == null) throw new Exception($"Součástka s ID {id} nebyla nalezena.");

            if (component.Quantity < amount)
            {
                throw new InvalidOperationException($"Nelze vyskladnit {amount} ks. Skladem je pouze {component.Quantity} ks.");
            }

            component.Quantity -= amount;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ComponentId = id,
                DeltaQuantity = -amount,
                Type = InventoryTransactionType.Use,
                Note = note,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task<int> DuplicateAsync(int id)
        {
            var original = await _db.Components
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (original == null) return 0;

            var copy = new Component
            {
                Name = original.Name + " (Kopie)",
                Manufacturer = original.Manufacturer,
                ManufacturerPartNumber = original.ManufacturerPartNumber,
                Package = original.Package,
                Quantity = 0, 
                ReorderPoint = original.ReorderPoint,
                LocationId = original.LocationId,
                IsActive = true,
                Documents = new List<Document>()
            };

            if (!string.IsNullOrEmpty(original.ImagePath))
            {
                var oldPath = Path.Combine(_env.WebRootPath, original.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath))
                {
                    var ext = Path.GetExtension(oldPath);
                    var newFileName = Guid.NewGuid().ToString() + "_copy" + ext;
                    var newPath = Path.Combine(_env.WebRootPath, "uploads", "components", newFileName);

                    System.IO.File.Copy(oldPath, newPath);
                    copy.ImagePath = "/uploads/components/" + newFileName;
                }
            }

            if (original.Documents != null && original.Documents.Any())
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "components");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var doc in original.Documents)
                {
                    var oldDocPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldDocPath))
                    {
                        var newFileName = Guid.NewGuid().ToString() + "_" + doc.FileName;
                        var newDocPath = Path.Combine(uploadsFolder, newFileName);

                        System.IO.File.Copy(oldDocPath, newDocPath);

                        copy.Documents.Add(new Document
                        {
                            FileName = doc.FileName,
                            FilePath = "/uploads/components/" + newFileName,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _db.Components.Add(copy);
            await _db.SaveChangesAsync();

            return copy.Id;
        }

        public async Task<(int importedCount, List<string> errors)> ImportFromCsvAsync(Stream fileStream)
        {
            var errors = new List<string>();
            int importedCount = 0;

            using var reader = new StreamReader(fileStream);
            bool isHeader = true;
            int lineNumber = 0;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                var values = line.Split(';');

                if (values.Length < 1 || string.IsNullOrWhiteSpace(values[0]))
                {
                    errors.Add($"Řádek {lineNumber}: Chybí název součástky.");
                    continue;
                }

                try
                {
                    var component = new Component
                    {
                        Name = values[0].Trim(),
                        Manufacturer = values.Length > 1 ? values[1].Trim() : null,
                        ManufacturerPartNumber = values.Length > 2 ? values[2].Trim() : null,
                        Quantity = values.Length > 3 && int.TryParse(values[3], out var q) ? q : 0,
                        ReorderPoint = values.Length > 4 && int.TryParse(values[4], out var rp) ? rp : 5,

                        IsActive = true
                    };

                    _db.Components.Add(component);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Řádek {lineNumber}: {ex.Message}");
                }
            }

            if (importedCount > 0)
            {
                await _db.SaveChangesAsync();
            }

            return (importedCount, errors);
        }
    }
}
