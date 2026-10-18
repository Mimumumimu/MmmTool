using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Console;
using MmmSdk.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;
using MmmToolWeb.Auth.Core.Passkeys;
using MmmToolWeb.Data.SqlServer;
using MmmToolWeb.Features;
using MmmToolWeb.Messages;
using MmmToolWeb.Notes;
using MmmToolWeb.Server.Auth;
using MmmToolWeb.Server.Commands;
using MmmToolWeb.Server.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 要求ごとの部品 (今のユーザー)を、全体で使う部品が抱えると、ほかの人のユーザーが混ざる。本番でも、起動のときに見つける
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// 外から見えるアドレス (Tailscale Funnel など)。パスキーは、このアドレスに結びついて作られる
var publicOrigin = new Uri(builder.Configuration["Web:PublicOrigin"] ?? "http://localhost:5080");

// 設定 (AppSettings.json)と Cookie の暗号化の鍵の置き場は、実行ファイルのフォルダの Data (DB のパスワードは資格情報マネージャー)
var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

builder.Services.AddMmmSdkCore(dataDirectory);
builder.Services.AddMmmToolWebSqlServer();

// ログは Data\Logs に書く。窓には警告以上だけを出す (通常のアクセスは、アクセスログに書く)
var logDirectory = Path.Combine(dataDirectory, "Logs");
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
});
builder.Logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddProvider(new FileLoggerProvider(logDirectory));

// アクセスログ (W3C 形式): アクセスのたびに 1 行。ファイルの大きさと数に上限がある。Cookie は書かない (ログインの値が、そのままファイルに残るため)
builder.Services.AddW3CLogging(options =>
{
    options.LogDirectory = logDirectory;
    options.FileName = "access-";
    options.LoggingFields = W3CLoggingFields.Date | W3CLoggingFields.Time | W3CLoggingFields.ClientIpAddress
        | W3CLoggingFields.Method | W3CLoggingFields.UriStem | W3CLoggingFields.ProtocolStatus
        | W3CLoggingFields.TimeTaken | W3CLoggingFields.UserAgent | W3CLoggingFields.RequestHeaders;
    options.AdditionalRequestHeaders.Add("X-Forwarded-For");
    options.AdditionalRequestHeaders.Add("X-Original-For");
    options.RetainedFileCountLimit = 60;
});

// Funnel は、同じ PC の localhost から転送してくる。そのときだけ、X-Forwarded-For の最後の 1 つ (Funnel が足した接続元)を、本当の接続元として使う
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Cookie の暗号化の鍵は、アプリ名と置き場を固定する (既定は実行ファイルのフォルダで決まり、発行先を替えると全員のログインが切れるため)
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("MmmToolWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "Keys")));
if (OperatingSystem.IsWindows())
{
    // 鍵のファイルを、サーバーを動かす Windows ユーザーでだけ読めるように暗号化する
    dataProtection.ProtectKeysWithDpapi();
}

builder.Services.AddFido2(options =>
{
    options.RPID = publicOrigin.Host;
    options.RPName = "MmmToolWeb";
    options.Origins = new HashSet<string> { publicOrigin.GetLeftPart(UriPartial.Authority) };
});

// 今のユーザーは、要求ごとに作り、ログインの Cookie から決める (ActiveUserMiddleware)
builder.Services.AddScoped<CurrentUser>();

// 機能。足すときに、サーバー本体で書くのは、この 1 行だけ
builder.Services.AddWebFeature<NotesWebFeature>();
builder.Services.AddWebFeature<MessagesWebFeature>();

builder.Services.AddSingleton<PasskeyCeremonyStore>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddSingleton<EnrollmentCodeService>();
builder.Services.AddSingleton<WebUserLookup>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "mmmtoolweb.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = AuthEndpoints.SessionLifetime;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// 探りを繰り返す接続元を断る。断った接続元の一覧は、ファイルに残す
builder.Services.AddSingleton<ProbeGuard>();
builder.Services.AddSingleton<ClientRoutes>();
builder.Services.AddHostedService(services => new RejectedClientListService(
    services.GetRequiredService<ProbeGuard>(), Path.Combine(logDirectory, "rejected-clients.txt")));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // 回数制限で断った要求も、断った接続元の一覧に数える
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.RequestServices.GetRequiredService<ProbeGuard>().RecordRejection(
            ClientAddress.PartitionKey(context.HttpContext), context.HttpContext.Request.Path.Value ?? string.Empty, DateTimeOffset.UtcNow);
        return ValueTask.CompletedTask;
    };

    // 登録・ログインの 4 つの口は、接続元ごとに数える (全員で共有すると、スキャナーが連打しただけで、本人のログインも止まるため)
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        ClientAddress.PartitionKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // 登録の口は、接続元を問わず、全体でも数える (接続元を偽られても、多数の接続元から試されても、登録用コードの総当たりの回数に上限を残すため)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments(AuthEndpoints.EnrollPath)
            ? RateLimitPartition.GetFixedWindowLimiter(
                "enroll",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("other"));
});

var app = builder.Build();

// サーバーの PC で行う操作 (サーバーは起動せず、終わったら閉じる)
switch (args)
{
    case [SetupDatabaseCommand.Name]:
        return await SetupDatabaseCommand.RunAsync(app.Services);
    case [IssueEnrollmentCodeCommand.Name, var loginName]:
        return await IssueEnrollmentCodeCommand.RunAsync(app.Services, loginName);
}

app.Logger.LogInformation("公開アドレス: {Origin}", publicOrigin);

// 順番: 本当の接続元を決める → ブラウザの守りの指定 → アクセスログ → 探りの接続元を断る → DB の失敗の受け止め → 画面のファイル → 守りの記録 → 回数制限 (429 も記録の対象にするため、記録のあと)
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseW3CLogging();
app.UseMiddleware<ProbeGuardMiddleware>();
app.UseMiddleware<DatabaseErrorMiddleware>();
app.UseBlazorFrameworkFiles();
// 画面のファイル (js など)は、使うたびに更新の有無を確かめさせる。端末に古い js が残ったまま、新しい画面と組み合わさらないため
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});
app.UseMiddleware<AuthAuditMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapWebFeatures();
app.MapFallbackToFile("index.html");

app.Run();
return 0;
