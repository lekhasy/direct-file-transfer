var builder = WebApplication.CreateBuilder(args);

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddSingleton<FileHasher>();

// Build AppConfig manually from configuration
var appConfig = new AppConfig {
    ServerPort = builder.Configuration.GetValue<int>("ServerPort"),
    FileDirectory = builder.Configuration.GetValue<string>("FileDirectory")
};
// Validate required config values
if (appConfig.ServerPort == 0)
    throw new Exception("Missing required configuration value: ServerPort");
if (string.IsNullOrWhiteSpace(appConfig.FileDirectory))
    throw new Exception("Missing required configuration value: FileDirectory");

builder.Services.AddSingleton(appConfig);
//builder.Services.AddSingleton<WatchFolderProvider>();
//builder.Services.AddSingleton<FileIndexCache>();

var app = builder.Build();

// Read ServerPort from appsettings.json
var config = app.Services.GetRequiredService<AppConfig>();
app.Urls.Add($"http://0.0.0.0:{config.ServerPort}");

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

// app.UseHttpsRedirection();

app.MapControllers();

app.Run();

