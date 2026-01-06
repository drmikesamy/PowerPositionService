using Services;
using Microsoft.Extensions.Options;

namespace PowerPositionService
{
    public class PowerPositionWorker(ILogger<PowerPositionWorker> _logger, IOptions<PowerPositionSettings> _settings, IPowerService _powerService) : BackgroundService
    {
        private readonly PowerPositionSettings _settings = _settings.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Running Generate Power Positions Job: {time} (Local time)", DateTime.Now);
                }
                await GeneratePowerPosition();
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMins), stoppingToken);
            }
        }
        public async Task GeneratePowerPosition()
        {
            try
            {
                var trades = await _powerService.GetTradesAsync(DateTime.Now);

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

                await GenerateCSV(report, DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating power position");
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
