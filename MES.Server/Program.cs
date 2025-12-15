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
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        app.Services.GetRequiredService<PlcConnector>();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "PLC 초기화에 실패했습니다.");
    }
});

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

try
{
    app.Run();
}
catch (IOException ex) when (IsAddressAlreadyInUse(ex))
{
    Log.Fatal(ex, "포트 5010이 이미 사용 중입니다. 실행 중인 MES.Server/IIS Express를 종료 후 다시 실행하세요.");
}
finally
{
    Log.CloseAndFlush();
}

static bool IsAddressAlreadyInUse(IOException ex)
{
    if (ex.InnerException is Microsoft.AspNetCore.Connections.AddressInUseException)
    {
        return true;
    }

    return ex.InnerException is System.Net.Sockets.SocketException socketException &&
           socketException.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse;
}