var builder = WebApplication.CreateBuilder(args);

// logs folder + file name (YYYY-MM-DD)
var logPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "logs",
    $"MES.Server_{DateTime.Now:yyyy-MM-dd}.log");

// Serilog configuration (console + file)
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    //.MinimumLevel.Debug() // 필요 시 로그 레벨을 Debug로 조정
    .WriteTo.Async(c => c.Console())
    .WriteTo.Async(c => c.File(
        path: logPath,
        shared: true,
        rollingInterval: RollingInterval.Infinite))
    .CreateLogger();

// Use Serilog as ASP.NET Core logger
builder.Host.UseSerilog();

// --- Services ---
builder.Services.AddRazorPages();

// PLC 및 도메인 서비스 등록
builder.Services.AddSingleton<IPlcClient, MitsubishiPlcClient>();
builder.Services.AddSingleton<IProductionService, ProductionService>();
builder.Services.AddSingleton<IOrderService, OrderDbService>();
builder.Services.AddSingleton<IPlcSignalProcessor, PlcSignalProcessor>();
builder.Services.AddSingleton<IPlcSignalMonitor, PlcSignalMonitor>();
builder.Services.AddSingleton<PlcConnector>();
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

// 애플리케이션 시작 시 PLC 연결 및 모니터링 시작
app.Services.GetRequiredService<PlcConnector>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
