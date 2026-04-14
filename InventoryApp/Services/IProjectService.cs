using InventoryApp.Models;

namespace InventoryApp.Services
{
    public interface IProjectService
    {
        Task<List<Project>> GetAllAsync();
        Task<Project?> GetByIdAsync(int id);
        Task<Project?> GetDetailsAsync(int id); 
        Task CreateAsync(Project project);
        Task UpdateAsync(Project project);
        Task DeleteAsync(int id);
        bool ProjectExists(int id);

        Task AddItemAsync(int projectId, int? componentId, string? customName, int quantity, ProjectItemType type = ProjectItemType.Standard);
        Task DeleteItemAsync(int itemId);

        Task FindOffersAsync(int projectId); 
        Task SelectOfferAsync(int offerId);  
        Task AutoSelectCheapestAsync(int projectId); 

        Task ConsumeStockAsync(int projectId); 
        Task<byte[]> GenerateOrderCsvAsync(int projectId, string? supplierName = null);
        Task UploadFileAsync(int projectId, IFormFile file);
        Task DeleteFileAsync(int fileId);
        Task ToggleItemFulfillmentAsync(int itemId);
        Task<int> DuplicateProjectAsync(int originalId);
        Task<byte[]> GenerateSupplierPdfAsync(int projectId, string supplierName);
        Task<List<Project>> GetDashboardActiveProjectsAsync(int limit = 6);
    }
}
