using Services;
using Microsoft.Extensions.Options;

namespace PowerPositionService
{
    public class Worker(ILogger<Worker> logger, IOptions<PowerPositionSettings> settings) : BackgroundService
    {
        private readonly PowerPositionSettings _settings = settings.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await GeneratePowerPosition();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);          
                }
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMins), stoppingToken);
            }
        }
        public async Task GeneratePowerPosition()
        {
            try
            {
                PowerService powerService = new PowerService();
                var trades = await powerService.GetTradesAsync(DateTime.Now);

                Dictionary<DateTime, Double> report = new Dictionary<DateTime, Double>();
                DateTime startTime = DateTime.Now.AddDays(-1).Date.AddHours(23);

                foreach (var trade in trades)
                {
                    for (int i = 0; i < 24; i++)
                    {
                        double periodVolume = trade.Periods[i].Volume;
                        DateTime correspondingDate = startTime.AddHours(i);

                        if (!report.ContainsKey(correspondingDate))
                        {
                            report[correspondingDate] = 0;
                        }

                        report[correspondingDate] += periodVolume;
                    }
                }
                foreach (var entry in report)
                {
                    logger.LogInformation("Local Time: {time}, Volume: {volume}", entry.Key.ToString("HH:mm"), entry.Value);
                }

                await GenerateCSV(report, DateTime.Now);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating power position");
            }
        }
        public async Task GenerateCSV(Dictionary<DateTime, Double> report, DateTime generatedDate)
        {
            if (!Directory.Exists(_settings.CsvOutputDir))
            {
                Directory.CreateDirectory(_settings.CsvOutputDir);
            }

            string filePath = Path.Combine(_settings.CsvOutputDir, $"PowerPosition_{generatedDate.ToString("yyyyMMdd_HHmm")}.csv");
            string csvHeaderLine = $"Local Time,Volume";
            await File.AppendAllTextAsync(filePath, csvHeaderLine + Environment.NewLine);
            foreach (var entry in report)
            {
                string csvLine = $"{entry.Key.ToString("HH:mm")},{entry.Value}";
                await File.AppendAllTextAsync(filePath, csvLine + Environment.NewLine);
            }
        }
    }
}
