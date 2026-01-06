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
                await GeneratePowerPosition();
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMins), stoppingToken);
            }
        }
        public async Task GeneratePowerPosition()
        {
            _logger.LogInformation("Running Generate Power Positions Job: {time} (Local time)", DateTime.Now);
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
                _logger.LogInformation("Successfully completed Power Positions Job: {time} (Local time)", DateTime.Now);
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
