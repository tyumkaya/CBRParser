using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace CBRParser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            int daysBack = 90;
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-daysBack + 1);

            HttpClient client = new HttpClient();

            List<CurrencyRate> allRates = new List<CurrencyRate>();

            var loadingCts = new CancellationTokenSource();
            var loadingTask = ShowLoadingAnimation(loadingCts.Token);

            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                string url = $"http://www.cbr.ru/scripts/XML_daily_eng.asp?date_req={date:dd/MM/yyyy}";
                try
                {
                    var responseBytes = await client.GetByteArrayAsync(url);
                    // Декодирую как windows-1251, потому что иначе выбивало исключение The character set provided in ContentType is invalid.
                    string response = Encoding.GetEncoding("windows-1251").GetString(responseBytes);
                    var xml = XDocument.Parse(response);

                    var rates = xml.Descendants("Valute")
                                   .Select(x => new CurrencyRate
                                   {
                                       Name = x.Element("Name").Value,
                                       CharCode = x.Element("CharCode").Value,
                                       Value = decimal.Parse(x.Element("VunitRate").Value, new CultureInfo("ru-RU")),
                                       Date = date
                                   });

                    allRates.AddRange(rates);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении данных за {date:dd/MM/yyyy}: {ex.Message}");
                }
            }

            loadingCts.Cancel();
            await loadingTask;

            if (allRates.Count == 0)
            {
                Console.WriteLine("Не удалось получить данные.");
                return;
            }

            Console.Clear();

            // Максимальный курс
            var maxRate = allRates.OrderByDescending(r => r.Value).First();
            Console.WriteLine($"Максимальный курс: {maxRate.Value} ({maxRate.Name}, {maxRate.CharCode}) на дату {maxRate.Date:dd/MM/yyyy}");

            // Минимальный курс
            var minRate = allRates.OrderBy(r => r.Value).First();
            Console.WriteLine($"Минимальный курс: {minRate.Value} ({minRate.Name}, {minRate.CharCode}) на дату {minRate.Date:dd/MM/yyyy}");

            // Средний курс по всем валютам и всем дням
            var averageRate = allRates.Average(r => r.Value);
            Console.WriteLine($"Средний курс за {daysBack} дней по всем валютам: {averageRate:F4}");

            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadLine();
        }

        static async Task ShowLoadingAnimation(CancellationToken token)
        {
            string baseText = "Загрузка результатов";
            int dotCount = 0;
            while (!token.IsCancellationRequested)
            {
                dotCount = (dotCount % 3) + 1;
                string dots = new string('.', dotCount);
                Console.Write($"\r{baseText}{dots}   ");
                await Task.Delay(500);
            }
        }
    }

    class CurrencyRate
    {
        public string Name { get; set; }
        public string CharCode { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }
}
