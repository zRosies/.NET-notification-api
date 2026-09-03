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
builder.Services.AddHttpClient<BrevoNotificationProvider>();
builder.Services.AddHttpClient<SlackNotificationProvider>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationProvider>(sp =>
    new CompositeNotificationProvider(
    [
        sp.GetRequiredService<BrevoNotificationProvider>(),
        sp.GetRequiredService<SlackNotificationProvider>()
    ]));
builder.Services.AddHostedService<RabbitMqNotificationConsumerService>();
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
    db.Database.ExecuteSqlRaw("ALTER TABLE \"Notifications\" ALTER COLUMN \"Message\" TYPE text;");
    db.Database.ExecuteSqlRaw("""
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'Notifications'
                  AND column_name = 'Type'
                  AND data_type = 'character varying'
            ) THEN
                ALTER TABLE "Notifications" ALTER COLUMN "Type" TYPE integer
                USING CASE LOWER(TRIM("Type"))
                    WHEN 'order_created' THEN 0
                    WHEN 'payment_succeeded' THEN 1
                    WHEN 'payment_failed' THEN 2
                    ELSE "Type"::integer
                END;
            END IF;
        END $$;
        """);
    db.Database.ExecuteSqlRaw("ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"EntityId\" text;");
    db.Database.ExecuteSqlRaw("ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"Metadata\" text;");
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

