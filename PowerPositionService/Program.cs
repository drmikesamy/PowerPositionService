using PowerPositionService;
using System.Globalization;

var ukCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = ukCulture;
CultureInfo.DefaultThreadCurrentUICulture = ukCulture;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
