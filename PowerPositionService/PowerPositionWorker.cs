using Microsoft.Extensions.Options;
using Services;
using System.Text;

namespace PowerPositionService
{
    public class PowerPositionWorker(ILogger<PowerPositionWorker> _logger, IOptions<PowerPositionSettings> _settings, IPowerService _powerService) : BackgroundService
    {
        private readonly PowerPositionSettings _settings = _settings.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await GeneratePowerPosition(DateTime.Now);
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMins), stoppingToken);
            }
        }
        public async Task GeneratePowerPosition(DateTime extractTime)
        {
            _logger.LogInformation("Running Generate Power Positions Job: {time} (Local time)", extractTime);

            try
            {
                IEnumerable<PowerTrade> trades = default!;
                int retryCount = 0;
                while (trades == null && retryCount < 3)
                {
                    try
                    {
                        trades = await _powerService.GetTradesAsync(extractTime);
                    }
                    catch
                    {
                        retryCount++;
                        if (retryCount == 3) throw;
                        await Task.Delay(1000);
                    }
                }

                Dictionary<DateTime, Double> report = new Dictionary<DateTime, Double>();
                DateTime startTime = extractTime.AddDays(-1).Date.AddHours(23);

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

                await GenerateCSV(report, extractTime);
                _logger.LogInformation("Successfully completed Power Positions Job: {time} (Local time)", extractTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing Power Positions Job");
            }
        }
        private async Task GenerateCSV(Dictionary<DateTime, Double> report, DateTime generatedDate)
        {
            if (!Directory.Exists(_settings.CsvOutputDir))
            {
                Directory.CreateDirectory(_settings.CsvOutputDir);
            }

            string fileName = $"PowerPosition_{generatedDate:yyyyMMdd_HHmm}.csv";
            string filePath = Path.Combine(_settings.CsvOutputDir, fileName);

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Local Time,Volume");

            foreach (var entry in report.OrderBy(x => x.Key))
            {
                csvBuilder.AppendLine($"{entry.Key:HH:mm},{entry.Value}");
            }

            await File.WriteAllTextAsync(filePath, csvBuilder.ToString());
        }
    }
}
