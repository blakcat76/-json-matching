using System.Text.Json;
using FuzzySharp;

namespace JsonMatching
{
    class Program
    {
        // Models for JSON deserialization
        class ItemsType2Root
        {
            public List<Item>? items { get; set; }
        }

        class Item
        {
            public int id { get; set; }
            public string? name { get; set; }
        }

        class Product
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? Region { get; set; }
        }

        class ResultItem
        {
            public int Id { get; set; }
            public string? ItemName { get; set; }
            public string? ProductName { get; set; }
        }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Запуск сопоставления JSON-файлов...");

                // Define file paths
                string itemsFilePath = "itemsType2.Json";
                string productsFilePath = "products_page_279.Json";
                string resultFilePath = "result.json";

                // Check if input files exist
                if (!File.Exists(itemsFilePath))
                {
                    Console.WriteLine($"Ошибка: Файл '{itemsFilePath}' не найден.");
                    return;
                }

                if (!File.Exists(productsFilePath))
                {
                    Console.WriteLine($"Ошибка: Файл '{productsFilePath}' не найден.");
                    return;
                }

                // Read and parse itemsType2.Json
                string itemsJson = File.ReadAllText(itemsFilePath);
                ItemsType2Root? itemsData;
                
                try
                {
                    itemsData = JsonSerializer.Deserialize<ItemsType2Root>(itemsJson);
                    if (itemsData?.items == null)
                    {
                        Console.WriteLine($"Ошибка: Некорректный формат файла '{itemsFilePath}'.");
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Ошибка при парсинге '{itemsFilePath}': {ex.Message}");
                    return;
                }

                // Read and parse products_page_279.Json
                string productsJson = File.ReadAllText(productsFilePath);
                List<Product>? productsData;
                
                try
                {
                    productsData = JsonSerializer.Deserialize<List<Product>>(productsJson);
                    if (productsData == null)
                    {
                        Console.WriteLine($"Ошибка: Некорректный формат файла '{productsFilePath}'.");
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Ошибка при парсинге '{productsFilePath}': {ex.Message}");
                    return;
                }

                // Perform fuzzy matching
                List<ResultItem> results = new List<ResultItem>();
                int threshold = 80; // 80% similarity threshold

                Console.WriteLine($"\nПоиск совпадений с порогом схожести {threshold}%...\n");

                foreach (var item in itemsData.items)
                {
                    if (string.IsNullOrEmpty(item.name))
                        continue;

                    int bestScore = 0;
                    Product? bestMatch = null;

                    // Find the best matching product for this item
                    foreach (var product in productsData)
                    {
                        if (string.IsNullOrEmpty(product.Name))
                            continue;

                        // Calculate similarity ratio using FuzzySharp
                        int similarityScore = Fuzz.Ratio(item.name, product.Name);

                        if (similarityScore >= threshold && similarityScore > bestScore)
                        {
                            bestScore = similarityScore;
                            bestMatch = product;
                        }
                    }

                    // Add only the best match if found
                    if (bestMatch != null)
                    {
                        Console.WriteLine($"Совпадение найдено! (схожесть: {bestScore}%)");
                        Console.WriteLine($"  ID: {item.id}");
                        Console.WriteLine($"  ItemsType2: {item.name}");
                        Console.WriteLine($"  Products: {bestMatch.Name}\n");

                        results.Add(new ResultItem
                        {
                            Id = item.id,
                            ItemName = item.name,
                            ProductName = bestMatch.Name
                        });
                    }
                }

                // Generate result.json
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string resultJson = JsonSerializer.Serialize(results, options);
                File.WriteAllText(resultFilePath, resultJson);

                Console.WriteLine($"Результаты сохранены в '{resultFilePath}'.");
                Console.WriteLine($"Всего найдено совпадений: {results.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла непредвиденная ошибка: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
