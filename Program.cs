
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        "logs/mdm-.log",
        rollingInterval: RollingInterval.Day));

// Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AppDb")));

// Identity configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(2);

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Authentication cookie configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Data Protection configuration
builder.Services.AddDataProtection()
    .SetApplicationName("MdmBilling")
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            builder.Configuration["DataProtection:KeysPath"] ?? "keys"));

// Dependency injection
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<UserStatusFilter>();
builder.Services.AddScoped<UserService>();

// Phase 3: Server Management
builder.Services.AddSingleton<CredentialProtector>();
builder.Services.AddScoped<ServerService>();

// Batch Management
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddSingleton<ExcelBatchReader>();
builder.Services.AddScoped<BatchService>();

// Phase 5: Processing
builder.Services.Configure<ProcessingOptions>(
    builder.Configuration.GetSection("Processing"));

builder.Services.AddSingleton<ProcessingQueue>();
builder.Services.AddHostedService<ProcessingWorker>();
builder.Services.AddScoped<ProcessingService>();

// Phase 6: Real and Fake UDIL configuration
builder.Services.Configure<MdmBilling.Services.Udil.UdilOptions>(
    builder.Configuration.GetSection("Udil"));

builder.Services.AddSingleton<
    MdmBilling.Services.Udil.UdilConnectionFactory>();

if (builder.Configuration.GetValue<bool>("Udil:UseFake"))
{
    builder.Services.AddScoped<
        MdmBilling.Services.Udil.IUdilReader,
        MdmBilling.Services.Udil.FakeUdilReader>();

    builder.Services.AddScoped<
        MdmBilling.Services.Udil.IMeterServerLocator,
        MdmBilling.Services.Udil.FakeMeterServerLocator>();
}
else
{
    builder.Services.AddScoped<
        MdmBilling.Services.Udil.IUdilReader,
        MdmBilling.Services.Udil.UdilReader>();

    builder.Services.AddScoped<
        MdmBilling.Services.Udil.IMeterServerLocator,
        MdmBilling.Services.Udil.UdilMeterServerLocator>();
}

builder.Services.AddSingleton<
    MdmBilling.Services.Reports.ExcelReportWriter>();

builder.Services.AddSingleton<
    MdmBilling.Services.Reports.PdfReportWriter>();

builder.Services.AddScoped<
    MdmBilling.Services.ReportService>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<EmailService>();


// MVC configuration
builder.Services.AddControllersWithViews(options =>
{
    // Require authentication for all pages.
    options.Filters.Add(
        new AuthorizeFilter(
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()));

    // Blocks deactivated users and handles forced password changes.
    options.Filters.AddService<UserStatusFilter>();
});

var app = builder.Build();

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseStatusCodePages();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database
await DbSeeder.SeedAsync(
    app.Services,
    app.Configuration);

app.Run();
