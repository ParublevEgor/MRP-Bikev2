using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MRP.Api.Data;
using System.Text.Json.Serialization;

var contentRoot = ResolveContentRoot();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

// При запуске exe напрямую launchSettings не применяется.
// Фиксируем порт 5249 как fallback, чтобы UI открывался по ожидаемому адресу.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5249");
}

// DB
builder.Services.AddDbContext<BikeContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));


builder.Services.AddControllers()
    .AddJsonOptions(x =>
    {
        x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MRP Bike API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseRouting();

// Swagger до статики, чтобы /swagger и /swagger/v1/swagger.json не перехватывались файлами из wwwroot
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MRP Bike API v1");
    options.RoutePrefix = "swagger";
});

//app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

// Без явного маршрута GET / часто не доходит до UseDefaultFiles из‑за endpoint routing.
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();

static string ResolveContentRoot()
{
    var current = Directory.GetCurrentDirectory();
    if (Directory.Exists(Path.Combine(current, "wwwroot")))
        return current;

    var candidate = AppContext.BaseDirectory;
    for (var i = 0; i < 6; i++)
    {
        if (Directory.Exists(Path.Combine(candidate, "wwwroot")))
            return candidate;

        var parent = Directory.GetParent(candidate);
        if (parent is null)
            break;

        candidate = parent.FullName;
    }

    return current;
}