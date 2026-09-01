using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using MyApi.Services;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
using MyApi.Data;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddScoped<INotificationService, NotificationService>();
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = builder.Environment.IsProduction() && !string.IsNullOrWhiteSpace(databaseUrl)
    ? DatabaseUrlParser.ToConnectionString(databaseUrl)
    : Environment.GetEnvironmentVariable("ConnectionStrings__Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("A database connection is required. Set ConnectionStrings__Default locally or DATABASE_URL in production.");
}

builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connectionString));
// Authentication and authorization are temporarily disabled for local testing.
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.Authority = Environment.GetEnvironmentVariable("JWT_AUTHORITY");
//         options.Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
//         options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
//     });
// builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
// app.UseAuthentication();
// app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    db.Database.EnsureCreated();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

