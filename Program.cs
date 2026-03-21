using Microsoft.EntityFrameworkCore;
using MRP.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<BikeContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));


builder.Services.AddControllers()
    .AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();