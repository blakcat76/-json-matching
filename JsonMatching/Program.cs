using System.Text.Json;
using System.Text.RegularExpressions;
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

        // Normalize text for better matching
        static string NormalizeForMatching(string text, bool isItemsType2 = false)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Normalize whitespace characters (\t, \n, etc.) to single spaces
            text = Regex.Replace(text, @"[\t\n\r]+", " ");
            
            // Normalize multiple spaces to single space
            text = Regex.Replace(text, @"\s+", " ");

            // For itemsType2, remove "Пиво бутылочное" and "Пиво разливное"
            if (isItemsType2)
            {
                text = Regex.Replace(text, @"Пиво\s+(бутылочное|разливное)\s*", "", RegexOptions.IgnoreCase);
                // Remove common abbreviations and words in itemsType2
                text = Regex.Replace(text, @"\bимп\.\s*", "", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\bПЭТ\b", "", RegexOptions.IgnoreCase);
            }

            // Remove parentheses and their contents (often contains packaging info)
            text = Regex.Replace(text, @"\([^)]*\)", " ");

            // Remove common country/region names for better matching
            string[] regions = { "Германия", "Мексика", "Нидерланды", "Бельгия", "Италия", "Сербия", "Бавария" };
            foreach (var region in regions)
            {
                text = Regex.Replace(text, @"\b" + region + @"\b", "", RegexOptions.IgnoreCase);
            }

            // Normalize number separators (. , space) - keep numbers but normalize separators
            // E.g., "0.5" "0,5" "0 5" all become "05" for better matching
            text = Regex.Replace(text, @"(\d)[\s\.,](\d)", "$1$2");

            // Normalize volume units - standardize to simple form
            text = Regex.Replace(text, @"(\d+)\s*л\b", "$1l", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(\d+)\s*мл\b", "$1ml", RegexOptions.IgnoreCase);

            // Normalize packaging terms
            // "ж/б" (tin can) corresponds to "in can" - both become "can"
            text = text.Replace("ж/б", "can");
            text = Regex.Replace(text, @"\bin\s+can\b", "can", RegexOptions.IgnoreCase);
            
            // Remove "ст" (glass bottle) and "bottle" as they don't help matching
            text = Regex.Replace(text, @"\b(ст|bottle)\b", "", RegexOptions.IgnoreCase);

            // Remove extra punctuation for better matching
            text = text.Replace(",", " ").Replace("/", " ").Replace(".", " ");

            // Remove common filler words
            text = Regex.Replace(text, @"\b(bier|beer|lager)\b", "", RegexOptions.IgnoreCase);

            // Final cleanup - trim and normalize spaces
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text.ToLower();
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

                // Check if result.json already exists
                if (File.Exists(resultFilePath))
                {
                    Console.WriteLine($"\nФайл '{resultFilePath}' найден. Загрузка существующих совпадений...\n");
                    
                    try
                    {
                        string existingResultJson = File.ReadAllText(resultFilePath);
                        var existingResults = JsonSerializer.Deserialize<List<ResultItem>>(existingResultJson);
                        
                        if (existingResults != null && existingResults.Count > 0)
                        {
                            results = existingResults;
                            Console.WriteLine($"Загружено {results.Count} существующих совпадений из '{resultFilePath}'.");
                            Console.WriteLine("Пропуск этапа поиска совпадений >= 80%.\n");
                        }
                        else
                        {
                            Console.WriteLine($"Файл '{resultFilePath}' пуст. Выполняется поиск совпадений...\n");
                            // Will proceed to calculate matches below
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Ошибка при загрузке '{resultFilePath}': {ex.Message}");
                        Console.WriteLine("Выполняется поиск совпадений заново...\n");
                        results = new List<ResultItem>();
                    }
                }

                // JSON serialization options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // Only perform matching if results weren't loaded from file
                if (results.Count == 0)
                {
                    Console.WriteLine($"\nПоиск совпадений с порогом схожести {threshold}%...\n");

                foreach (var item in itemsData.items)
                {
                    if (string.IsNullOrEmpty(item.name))
                        continue;

                    int bestScore = 0;
                    Product? bestMatch = null;
                    string? bestMatchNormalized = null;

                    // Normalize the item name once for this iteration
                    string normalizedItemName = NormalizeForMatching(item.name, isItemsType2: true);

                    // Find the best matching product for this item
                    foreach (var product in productsData)
                    {
                        if (string.IsNullOrEmpty(product.Name))
                            continue;

                        // Normalize the product name
                        string normalizedProductName = NormalizeForMatching(product.Name, isItemsType2: false);

                        // Calculate similarity ratio using FuzzySharp on normalized names
                        int similarityScore = Fuzz.Ratio(normalizedItemName, normalizedProductName);

                        if (similarityScore >= threshold && similarityScore > bestScore)
                        {
                            bestScore = similarityScore;
                            bestMatch = product;
                            bestMatchNormalized = normalizedProductName;
                        }
                    }

                    // Add only the best match if found
                    if (bestMatch != null)
                    {
                        Console.WriteLine($"Совпадение найдено! (схожесть: {bestScore}%)");
                        Console.WriteLine($"  ID: {item.id}");
                        Console.WriteLine($"  ItemsType2: {item.name}");
                        Console.WriteLine($"  Products: {bestMatch.Name}");
                        Console.WriteLine($"  Нормализовано: '{normalizedItemName}' <-> '{bestMatchNormalized}'\n");

                        results.Add(new ResultItem
                        {
                            Id = item.id,
                            ItemName = item.name,
                            ProductName = bestMatch.Name
                        });
                    }
                }

                    // Generate result.json only if we calculated new results
                    string resultJson = JsonSerializer.Serialize(results, options);
                    File.WriteAllText(resultFilePath, resultJson);

                    Console.WriteLine($"Результаты сохранены в '{resultFilePath}'.");
                    Console.WriteLine($"Всего найдено совпадений: {results.Count}");
                }
                else
                {
                    // Results were loaded from existing file
                    Console.WriteLine($"Используются существующие результаты: {results.Count} совпадений.");
                }

                // Generate result2.json with best matches for remaining items (excluding those already matched)
                Console.WriteLine($"\n=== Поиск лучших совпадений для оставшихся товаров ===\n");
                
                // Create sets of already matched IDs and ProductNames
                HashSet<int> matchedItemIds = new HashSet<int>(results.Select(r => r.Id));
                HashSet<string> matchedProductNames = new HashSet<string>(results.Where(r => r.ProductName != null).Select(r => r.ProductName!));

                List<ResultItem> results2 = new List<ResultItem>();

                foreach (var item in itemsData.items)
                {
                    // Skip items already matched in result.json
                    if (matchedItemIds.Contains(item.id))
                        continue;

                    if (string.IsNullOrEmpty(item.name))
                        continue;

                    int bestScore = 0;
                    Product? bestMatch = null;
                    string? bestMatchNormalized = null;

                    // Normalize the item name once for this iteration
                    string normalizedItemName = NormalizeForMatching(item.name, isItemsType2: true);

                    // Find the best matching product for this item (excluding already matched products)
                    foreach (var product in productsData)
                    {
                        if (string.IsNullOrEmpty(product.Name))
                            continue;

                        // Skip products already matched in result.json
                        if (matchedProductNames.Contains(product.Name))
                            continue;

                        // Normalize the product name
                        string normalizedProductName = NormalizeForMatching(product.Name, isItemsType2: false);

                        // Calculate similarity ratio using FuzzySharp on normalized names
                        int similarityScore = Fuzz.Ratio(normalizedItemName, normalizedProductName);

                        // No threshold - just find the best match
                        if (similarityScore > bestScore)
                        {
                            bestScore = similarityScore;
                            bestMatch = product;
                            bestMatchNormalized = normalizedProductName;
                        }
                    }

                    // Add the best match found (even if score is low)
                    if (bestMatch != null)
                    {
                        Console.WriteLine($"Лучшее совпадение найдено! (схожесть: {bestScore}%)");
                        Console.WriteLine($"  ID: {item.id}");
                        Console.WriteLine($"  ItemsType2: {item.name}");
                        Console.WriteLine($"  Products: {bestMatch.Name}");
                        Console.WriteLine($"  Нормализовано: '{normalizedItemName}' <-> '{bestMatchNormalized}'\n");

                        results2.Add(new ResultItem
                        {
                            Id = item.id,
                            ItemName = item.name,
                            ProductName = bestMatch.Name
                        });

                        // Mark this product as matched so it won't be used again
                        if (bestMatch.Name != null)
                            matchedProductNames.Add(bestMatch.Name);
                    }
                }

                // Generate result2.json
                string result2FilePath = "result2.json";
                string result2Json = JsonSerializer.Serialize(results2, options);
                File.WriteAllText(result2FilePath, result2Json);

                Console.WriteLine($"Дополнительные результаты сохранены в '{result2FilePath}'.");
                Console.WriteLine($"Всего найдено дополнительных совпадений: {results2.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла непредвиденная ошибка: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
