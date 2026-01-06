using PowerPositionService;
using Services;
using System.Globalization;

var ukCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = ukCulture;
CultureInfo.DefaultThreadCurrentUICulture = ukCulture;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<PowerPositionSettings>(builder.Configuration);
builder.Services.AddSingleton<IPowerService, PowerService>();
builder.Services.AddHostedService<PowerPositionWorker>();

var host = builder.Build();
host.Run();
