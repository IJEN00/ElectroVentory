using InventoryApp.Models;
using InventoryApp.Services.Suppliers.Mouser.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InventoryApp.Services.Suppliers.Mouser
{
    public class MouserApiClient : ISupplierClient
    {
        public string SupplierName => "Mouser";
        public bool IsRealApi => true;

        private readonly HttpClient _httpClient;
        private readonly MouserApiOptions _options;

        public MouserApiClient(HttpClient httpClient, IOptions<MouserApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<List<SupplierOffer>> SearchAsync(ProjectItem item)
        {
            var offers = new List<SupplierOffer>();

            var c = item.Component;
            string symbol = string.Empty;

            if (c != null && !string.IsNullOrWhiteSpace(c.ManufacturerPartNumber))
            {
                symbol = c.ManufacturerPartNumber;
            }

            if (string.IsNullOrWhiteSpace(symbol) && !string.IsNullOrWhiteSpace(item.CustomName))
            {
                symbol = item.CustomName;
            }

            if (string.IsNullOrWhiteSpace(symbol))
                return offers;

            var requestObj = new MouserSearchRequestRoot
            {
                SearchByPartRequest = new MouserPartSearchRequest
                {
                    MouserPartNumber = symbol,
                    PartSearchOptions = "" 
                }
            };

            var json = JsonSerializer.Serialize(requestObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_options.BaseUrl}/search/partnumber?apiKey={_options.ApiKey}";

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                return offers;

            var responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine("MOUSER RESPONSE:");
            Console.WriteLine(responseJson);

            var data = JsonSerializer.Deserialize<MouserSearchResponse>(responseJson);
            if (data == null || data.SearchResults.Parts.Count == 0)
                return offers;

            var part = data.SearchResults.Parts.First();

            var requiredQty = item.QuantityToBuy > 0 ? item.QuantityToBuy : 0;
            if (requiredQty <= 0)
                return offers;

            int stockQty = 0;
            if (!int.TryParse(part.AvailabilityInStock, NumberStyles.Any, CultureInfo.InvariantCulture, out stockQty))
            {
                int.TryParse(part.FactoryStock, NumberStyles.Any, CultureInfo.InvariantCulture, out stockQty);
            }

            if (stockQty < requiredQty)
            {
                Console.WriteLine($"MOUSER: stock {stockQty} < required {requiredQty} for '{symbol}' (availability='{part.Availability}')");
                return offers;
            }

            decimal unitPrice = 0m;
            if (part.PriceBreaks != null && part.PriceBreaks.Any())
            {
                var raw = part.PriceBreaks.First().Price ?? "0";

                var match = Regex.Match(raw, @"[\d,\.]+");
                if (match.Success)
                {
                    string cleanNumber = match.Value.Replace(',', '.');

                    decimal.TryParse(cleanNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out unitPrice);
                }
            }

            var minOrderQty = 1;
            if (!string.IsNullOrWhiteSpace(part.Min))
                int.TryParse(part.Min, out minOrderQty);

            var offer = new SupplierOffer
            {
                ProjectItemId = item.Id,
                Description = part.Description,
                UnitPrice = unitPrice,
                Currency = "CZK", 
                InStock = stockQty > 0,
                MinOrderQty = minOrderQty,
                LeadTimeDays = null,
                ProductUrl = string.IsNullOrWhiteSpace(part.ProductDetailUrl)
                    ? $"https://www.mouser.com/ProductDetail/{part.MouserPartNumber}"
                    : part.ProductDetailUrl,
                SupplierPartNumber = part.MouserPartNumber
            };

            offers.Add(offer);
            return offers;
        }
    }
}
