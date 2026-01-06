using Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.RegularExpressions;

namespace PowerPositionService.Tests
{
    public class PowerPositionWorkerTests
    {
        private ILogger<PowerPositionWorker> _logger;
        private Mock<IPowerService> _powerServiceMock;
        private IOptions<PowerPositionSettings> _settings;
        private string _testOutputDir;

        [SetUp]
        public void Setup()
        {
            _logger = NullLogger<PowerPositionWorker>.Instance;
            _powerServiceMock = new Mock<IPowerService>();
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"PowerPositionTests_{Guid.NewGuid()}");

            _settings = Options.Create(new PowerPositionSettings
            {
                CsvOutputDir = _testOutputDir,
                IntervalMins = 1
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, true);
            }
        }

        [Test]
        public async Task GeneratePowerPosition_SuccessfullyGeneratesCsv()
        {
            // Arrange
            var trades = CreateMockTrades();

            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(trades);

            var worker = new PowerPositionWorker(_logger, _settings, _powerServiceMock.Object);

            // Act
            await worker.GeneratePowerPosition();

            // Assert
            var files = Directory.GetFiles(_testOutputDir, "PowerPosition_*.csv");
            var lines = await File.ReadAllLinesAsync(files[0]);

            Assert.That(lines.Length, Is.EqualTo(25));
            Assert.That(lines[1], Does.Contain("150"));
            Assert.That(lines[12], Does.Contain("80"));
        }

        [Test]
        public async Task GeneratePowerPosition_GeneratesCorrectFilenameFormat()
        {
            // Arrange
            var trades = CreateMockTrades();
            var beforeGeneration = DateTime.Now;

            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(trades);

            var worker = new PowerPositionWorker(_logger, _settings, _powerServiceMock.Object);

            // Act
            await worker.GeneratePowerPosition();
            var afterGeneration = DateTime.Now;

            // Assert
            var files = Directory.GetFiles(_testOutputDir, "PowerPosition_*.csv");
            Assert.That(files.Length, Is.EqualTo(1));

            var filename = Path.GetFileName(files[0]);
            var filenamePattern = @"^PowerPosition_\d{8}_\d{4}\.csv$";
            Assert.That(Regex.IsMatch(filename, filenamePattern), Is.True);

            var timestampMatch = Regex.Match(filename, @"PowerPosition_(\d{8})_(\d{4})\.csv");
            var dateStr = timestampMatch.Groups[1].Value;
            var timeStr = timestampMatch.Groups[2].Value;
            
            var fileDateTime = DateTime.ParseExact($"{dateStr}_{timeStr}", "yyyyMMdd_HHmm", null);
            Assert.That(fileDateTime, Is.GreaterThanOrEqualTo(beforeGeneration.AddMinutes(-1)));
            Assert.That(fileDateTime, Is.LessThanOrEqualTo(afterGeneration.AddMinutes(1)));
        }

        private List<PowerTrade> CreateMockTrades()
        {
            PowerTrade trade1 = PowerTrade.Create(new DateTime(2015, 04, 01), 24);

            for (int i = 0; i < 24; i++)
            {
                trade1.Periods[i] = new PowerPeriod { Period = i + 1, Volume = 100 };
            }

            PowerTrade trade2 = PowerTrade.Create(new DateTime(2015, 04, 01), 24);

            for (int i = 0; i < 11; i++)
            {
                trade2.Periods[i] = new PowerPeriod { Period = i + 1, Volume = 50 };
            }
            for (int i = 11; i < 24; i++)
            {
                trade2.Periods[i] = new PowerPeriod { Period = i + 1, Volume = -20 };
            }

            return new List<PowerTrade> { trade1, trade2 };
        }
    }
}