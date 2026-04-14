using InventoryApp.Models;
using InventoryApp.Services.Suppliers.TME.Models; 
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InventoryApp.Services.Suppliers.TME
{
    public class TMEApiClient : ISupplierClient
    {
        public string SupplierName => "TME";
        public bool IsRealApi => true;

        private readonly HttpClient _httpClient;
        private readonly TMEApiOptions _options;

        public TMEApiClient(HttpClient httpClient, IOptions<TMEApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        private async Task<string?> ResolveTmeSymbolAsync(ProjectItem item)
        {
            var searchPlain = BuildSearchQuery(item);
            if (string.IsNullOrWhiteSpace(searchPlain)) return null;

            if (!IsConfigured()) return null;

            const string endpoint = "Products/Search.json";
            var parameters = new Dictionary<string, string>
            {
                ["Language"] = "EN",
                ["Country"] = "CZ",
                ["SearchPlain"] = searchPlain,
                ["Token"] = _options.Token
            };

            var jsonResponse = await SendRequestAsync(endpoint, parameters);
            if (jsonResponse == null) return null;

            var responseObj = JsonSerializer.Deserialize<TmeResponse>(jsonResponse);

            if (responseObj?.Data?.ProductList == null || !responseObj.Data.ProductList.Any())
                return null;

            var bestProduct = responseObj.Data.ProductList.FirstOrDefault(); 

            if (!string.IsNullOrWhiteSpace(item.Component?.Manufacturer))
            {
                var match = responseObj.Data.ProductList
                    .FirstOrDefault(p => string.Equals(p.Producer, item.Component.Manufacturer, StringComparison.OrdinalIgnoreCase));

                if (match != null) bestProduct = match;
            }

            Console.WriteLine($"TME: pro '{searchPlain}' nalezen symbol '{bestProduct?.Symbol}'");
            return bestProduct?.Symbol;
        }

        public async Task<List<SupplierOffer>> SearchAsync(ProjectItem item)
        {
            var result = new List<SupplierOffer>();
            var symbol = await ResolveTmeSymbolAsync(item);

            if (string.IsNullOrWhiteSpace(symbol) || !IsConfigured()) return result;

            const string endpoint = "Products/GetPricesAndStocks.json";
            var parameters = new Dictionary<string, string>
            {
                ["Language"] = "EN",
                ["Country"] = "CZ",
                ["SymbolList[0]"] = symbol,
                ["Token"] = _options.Token
            };

            var jsonResponse = await SendRequestAsync(endpoint, parameters);
            if (jsonResponse == null) return result;

            var responseObj = JsonSerializer.Deserialize<TmeResponse>(jsonResponse);

            if (responseObj?.Data?.ProductList == null) return result;

            foreach (var product in responseObj.Data.ProductList)
            {
                int stockQty = product.Amount;
                int requiredQty = item.QuantityToBuy > 0 ? item.QuantityToBuy : 1;

                if (stockQty < requiredQty) continue;

                decimal unitPrice = 0m;
                if (product.PriceList.Any())
                {
                    unitPrice = product.PriceList.First().PriceValue;
                }

                var offer = new SupplierOffer
                {
                    ProjectItemId = item.Id,
                    Description = product.Symbol, 
                    UnitPrice = unitPrice,
                    Currency = responseObj.Data.Currency, 
                    InStock = stockQty > 0,
                    MinOrderQty = 1,
                    ProductUrl = $"https://www.tme.eu/cz/details/{product.Symbol}",
                    SupplierPartNumber = product.Symbol
                };

                result.Add(offer);
            }

            return result;
        }

        private async Task<string?> SendRequestAsync(string endpointPath, Dictionary<string, string> parameters)
        {
            try
            {
                var signature = TMESignatureHelper.CreateSignature(
                    _options.BaseUrl,
                    endpointPath,
                    parameters,
                    _options.Secret);

                parameters["ApiSignature"] = signature;

                var content = new FormUrlEncodedContent(parameters);
                var url = $"{_options.BaseUrl.TrimEnd('/')}/{endpointPath}";

                var response = await _httpClient.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"TME API Error ({response.StatusCode}): {json}");
                    return null;
                }

                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TME Connection Error: {ex.Message}");
                return null;
            }
        }

        private bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
                   !string.IsNullOrWhiteSpace(_options.Token) &&
                   !string.IsNullOrWhiteSpace(_options.Secret);
        }

        private string BuildSearchQuery(ProjectItem item)
        {
            if (item.Component != null && !string.IsNullOrWhiteSpace(item.Component.ManufacturerPartNumber))
                return item.Component.ManufacturerPartNumber;

            if (!string.IsNullOrWhiteSpace(item.CustomName))
                return item.CustomName;

            return string.Empty;
        }
    }
}