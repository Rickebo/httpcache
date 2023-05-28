using HttpCache.Settings;
using static HttpCache.Startup;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddHttpCache()
    .AddControllers();

if (builder.Configuration.GetSettings<PulsarSettings>()?.Enabled ?? false)
    builder.Services.AddPulsar();
    

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HttpCache}/{action=HandleRequest}/{maxTime?}"
);

app.Run();