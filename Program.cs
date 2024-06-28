using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Azure;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvc();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["ApplicationInsights:InstrumentationKey"]);
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("ConnectionStrings:BlobStorage")));
builder.Services.AddLogging();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.UseStaticFiles();

app.UseRouting();

app.MapControllers();

app.Run();
