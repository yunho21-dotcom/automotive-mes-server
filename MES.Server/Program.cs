var builder = WebApplication.CreateBuilder(args);

// logs folder + file name (YYYY-MM-DD)
var logPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "logs",
    $"MES.Server_{DateTime.Now:yyyy-MM-dd}.log");

// Serilog configuration (console + file)
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    //.MinimumLevel.Debug() // 가장 낮은 로그 레벨을 Debug로 설정
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

// PLC 및 주문 서비스 등록
builder.Services.AddSingleton<PlcConnector>();
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

// 애플리케이션 시작 시 PLC 연결 및 모니터링을 바로 시작
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
