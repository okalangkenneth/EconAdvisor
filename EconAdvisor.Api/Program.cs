using EconAdvisor.Api.Data;
using EconAdvisor.Api.Endpoints;
using EconAdvisor.Api.Services;
using EconAdvisor.Api.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console());

    // ── PostgreSQL / EF Core ─────────────────────────────────────────────────
    var connStr = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");
    builder.Services.AddDbContext<EconContext>(opt =>
        opt.UseNpgsql(connStr));

    // ── Typed HTTP clients ────────────────────────────────────────────────────
    var apis = builder.Configuration.GetSection("ExternalApis");

    builder.Services
        .AddHttpClient<RiksbankClient>(c =>
        {
            c.BaseAddress = new Uri(
                apis["RiksbankBase"] ?? "https://api.riksbank.se/swea/v1");
            // Ensure trailing slash so relative segments resolve correctly
            if (!c.BaseAddress.AbsolutePath.EndsWith('/'))
                c.BaseAddress = new Uri(c.BaseAddress + "/");
            c.DefaultRequestHeaders.Add("Accept", "application/json");
            c.Timeout = TimeSpan.FromSeconds(30);
        });

    builder.Services
        .AddHttpClient<ScbClient>(c =>
        {
            c.BaseAddress = new Uri(
                (apis["ScbBase"] ?? "https://api.scb.se/OV0104/v1/doris/sv/ssd") + "/");
            c.DefaultRequestHeaders.Add("Accept", "application/json");
            c.Timeout = TimeSpan.FromSeconds(30);
        });

    builder.Services
        .AddHttpClient<WorldBankClient>(c =>
        {
            c.BaseAddress = new Uri(
                (apis["WorldBankBase"] ?? "https://api.worldbank.org/v2") + "/");
            c.DefaultRequestHeaders.Add("Accept", "application/json");
            c.Timeout = TimeSpan.FromSeconds(30);
        });

    var dify = builder.Configuration.GetSection("Dify");
    var difyBase = dify["ApiBase"] ?? "http://localhost/v1";
    var difyKey  = dify["WorkflowKey"] ?? string.Empty;

    builder.Services
        .AddHttpClient<DifyWorkflowClient>(c =>
        {
            c.BaseAddress = new Uri(difyBase.TrimEnd('/') + "/");
            c.DefaultRequestHeaders.Add("Authorization", $"Bearer {difyKey}");
            c.Timeout = TimeSpan.FromSeconds(120); // workflows can be slow
        });

    // ── Validation ───────────────────────────────────────────────────────────
    builder.Services.AddValidatorsFromAssemblyContaining<AnalyseRequestValidator>();

    // ── Application services ─────────────────────────────────────────────────
    builder.Services.AddScoped<IndicatorService>();

    // ── Swagger / OpenAPI ────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "EconAdvisor API", Version = "v1" });
    });

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "postgres");

    // ── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // ── Endpoints ────────────────────────────────────────────────────────────
    app.MapHealthChecks("/health");
    app.MapIndicatorEndpoints();
    app.MapAnalyseEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
