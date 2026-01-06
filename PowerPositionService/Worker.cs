using Services;

namespace PowerPositionService
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await GeneratePowerPosition();
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);          
                }
                await Task.Delay(1000, stoppingToken);
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating power position");
            }
        }
    }
}
