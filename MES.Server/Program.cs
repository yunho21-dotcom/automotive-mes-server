var builder = WebApplication.CreateBuilder(args);

// logs 폴더 + 파일 이름 구성 (YYYY-MM-DD 형식)
var logPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "logs",
    $"MES.Server_{DateTime.Now:yyyy-MM-dd}.log");

// Serilog 전역 로거 구성 (비동기 싱크 사용)
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
//    .MinimumLevel.Debug() // 가장 낮은 로그 레벨을 Debug로 설정
    .WriteTo.Async(c => c.Console())               // 콘솔 출력(백그라운드 처리)
    .WriteTo.Async(c => c.File(
        path: logPath,
        shared: true,
        rollingInterval: RollingInterval.Infinite)) // 한 실행당 한 파일
    .CreateLogger();

// ASP.NET Core 전체가 Serilog를 사용하도록 설정
builder.Host.UseSerilog();

// --- 서비스 등록 단계 ---
builder.Services.AddRazorPages();

// PLC / 주문 서비스 등록
builder.Services.AddSingleton<PlcConnector>();   // 논리국번호 1번 기본 생성자 사용
builder.Services.AddSingleton<OrderService>();

// 백그라운드 주문 동작
builder.Services.AddHostedService<PlcOrderWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();