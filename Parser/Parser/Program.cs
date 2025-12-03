using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

class Program
{
    static async Task Main()
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Старт парсингу");

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");

        using var driver = new ChromeDriver(options);

        try
        {
            driver.Navigate().GoToUrl("https://outage.zakarpat.energy/schedule_queues");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
            wait.Until(d => d.FindElements(By.CssSelector(".group-images img")).Count > 0);

            await Task.Delay(2000);

            var imgElements = driver.FindElements(By.CssSelector(".group-images img"));
            var imageUrls = imgElements
                .Select(img => img.GetAttribute("src"))
                .Where(src => !string.IsNullOrEmpty(src) && src.Contains("GPV.png"))
                .Distinct()
                .ToList();

            Console.WriteLine($"Знайдено {imageUrls.Count} графіків");

            // Шлях до папки graphics (на рівень вище)
            var graphicsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "graphics");
            Directory.CreateDirectory(graphicsDir);

            // Видаляємо старі
            foreach (var oldFile in Directory.GetFiles(graphicsDir, "*.png"))
            {
                File.Delete(oldFile);
                Console.WriteLine($"Видалено старий: {Path.GetFileName(oldFile)}");
            }

            using var httpClient = new HttpClient();

            for (int i = 0; i < imageUrls.Count; i++)
            {
                var bytes = await httpClient.GetByteArrayAsync(imageUrls[i]);
                var filePath = Path.Combine(graphicsDir, $"grafic_{i + 1}.png");
                await File.WriteAllBytesAsync(filePath, bytes);
                Console.WriteLine($"✓ Збережено: grafic_{i + 1}.png ({bytes.Length / 1024} KB)");
            }

            // Час оновлення
            var updateFile = Path.Combine(graphicsDir, "last_update.txt");


            TimeZoneInfo kyivZone;
            try
            {
                kyivZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
            }
            catch (TimeZoneNotFoundException)
            {
                kyivZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при визначенні часового поясу: {ex.Message}. Використовується UTC.");
                await File.WriteAllTextAsync(updateFile, DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm UTC"));
                return;
            }

            var kyivTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyivZone);

            await File.WriteAllTextAsync(updateFile, kyivTime.ToString("dd.MM.yyyy HH:mm 'EET'"));

            Console.WriteLine($"\n✓ Готово! Збережено {imageUrls.Count} графіків. Час: {kyivTime:dd.MM.yyyy HH:mm EET}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка: {ex.Message}");
            throw;
        }
        finally
        {
            driver.Quit();
        }
    }
}