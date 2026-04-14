using InventoryApp.Models;
using InventoryApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _db;

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<LowStockRow>> GetLowStockReportAsync(string filter)
        {
            var components = await _db.Components
                .Include(c => c.Location)
                .Where(c => c.IsActive && c.Quantity < (c.ReorderPoint ?? 5))
                .OrderBy(c => c.Quantity)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var componentIds = components.Select(c => c.Id).ToList();

            var allAddTransactions = await _db.InventoryTransactions
                .Where(t => componentIds.Contains(t.ComponentId) && t.Type == InventoryTransactionType.Add)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var lastTransactions = allAddTransactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Note))
                .GroupBy(t => t.ComponentId)
                .ToDictionary(g => g.Key, g => g.First().Note);

            var rows = components
                .Select(c =>
                {
                    int reorderPoint = c.ReorderPoint ?? 5;
                    int qty = c.Quantity;

                    string? lastSupplier = null;
                    if (lastTransactions.TryGetValue(c.Id, out var note) && !string.IsNullOrWhiteSpace(note))
                    {
                        var parts = note.Split('-');
                        lastSupplier = parts[0].Trim();
                    }

                    return new LowStockRow
                    {
                        ComponentId = c.Id,
                        Name = c.Name,
                        ManufacturerPartNumber = c.ManufacturerPartNumber,
                        Quantity = qty,
                        ReorderPoint = reorderPoint,
                        ToBuy = Math.Max(reorderPoint - qty, 0),
                        LocationDisplay = c.Location != null ? c.Location.DisplayName : "–",
                        LastSupplier = lastSupplier 
                    };
                })
                .ToList();

            filter = (filter ?? "all").ToLowerInvariant();
            return filter switch
            {
                "out" => rows.Where(r => r.Quantity == 0).ToList(),
                "low" => rows.Where(r => r.Quantity > 0).ToList(),
                _ => rows
            };
        }

        public async Task<List<ConsumptionRow>> GetConsumptionReportAsync(int days, bool projectsOnly)
        {
            var q = _db.InventoryTransactions.AsNoTracking()
                .Where(t => t.DeltaQuantity < 0);

            if (days > 0)
            {
                var since = DateTime.UtcNow.AddDays(-days);
                q = q.Where(t => t.CreatedAt >= since);
            }

            if (projectsOnly)
                q = q.Where(t => t.ProjectId != null);

            var rows = await q
                .GroupBy(t => t.ComponentId)
                .Select(g => new ConsumptionRow
                {
                    ComponentId = g.Key,
                    UsedFromProjects = -g.Where(x => x.ProjectId != null).Sum(x => x.DeltaQuantity),
                    UsedManual = -g.Where(x => x.ProjectId == null).Sum(x => x.DeltaQuantity),
                    Used = -g.Sum(x => x.DeltaQuantity),
                    UsesCount = g.Count()
                })
                .OrderByDescending(r => r.Used)
                .Take(50)
                .ToListAsync();

            var ids = rows.Select(r => r.ComponentId).ToList();
            var components = await _db.Components
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.ManufacturerPartNumber, c.Quantity, ReorderPoint = (c.ReorderPoint ?? 5) })
                .ToListAsync();

            var map = components.ToDictionary(x => x.Id, x => x);

            foreach (var r in rows)
            {
                if (map.TryGetValue(r.ComponentId, out var c))
                {
                    r.Name = c.Name;
                    r.ManufacturerPartNumber = c.ManufacturerPartNumber;
                    r.Quantity = c.Quantity;
                    r.ReorderPoint = c.ReorderPoint;
                    r.ToBuy = Math.Max(c.ReorderPoint - c.Quantity, 0);
                }
            }

            return rows;
        }

        public async Task<List<Project>> GetProjectReportAsync()
        {
            var projects = await _db.Projects
                .Include(p => p.Items) 
                .ToListAsync();

            return projects
                .OrderBy(p => p.Status switch
                {
                    ProjectStatus.InProduction => 1, 
                    ProjectStatus.Planning => 2,     
                    ProjectStatus.ReadyToBuild => 3, 
                    ProjectStatus.Ordered => 4,      
                    ProjectStatus.Completed => 5,    
                    _ => 6
                })
                .ThenByDescending(p => p.CreatedAt) 
                .ToList();
        }
    }
}