using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Endpoints;
using ExpenseTracker.Api.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to platform-provided PORT (e.g. Render, Koyeb, Fly.io) when present
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<ExpenseDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Apply pending EF Core migrations automatically on startup
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ExpenseDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

var swaggerEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue("Swagger:Enabled", false);

if (swaggerEnabled)
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Expense Tracker API v1");
    });

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithTags("Health")
    .WithSummary("Health check endpoint");

app.MapExpenseEndpoints();

app.Run();

public partial class Program;
