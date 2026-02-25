using Microsoft.AspNetCore.Mvc;
using NIOP.Provider.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use PascalCase for consistent contract serialization across all consumers
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Suppress the automatic 400 ValidationProblemDetails response that ASP.NET Core produces
// when [Required] annotations fail. Instead, the request reaches the controller and
// DeviceService returns the correctly-formatted { Success, Message, CorrelationId } 400,
// which is what Pact consumers expect.
// [Required] is kept on the model for accurate OpenAPI/Swagger spec generation.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "NIOP Beat Inventory Client API",
        Version = "v1",
        Description = "API for managing device inventory, bundles, orders, and device information updates."
    });

    // Include XML documentation comments so Swagger spec carries full descriptions,
    // which swagger-mock-validator uses when comparing against Pact contracts.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register application services
builder.Services.AddScoped<IDeviceService, DeviceService>();

var app = builder.Build();

// Swagger is enabled in Development and in the Testing environment used by contract tests
// (swagger-mock-validator needs to fetch swagger.json from the running test server).
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();

// Make Program accessible for integration/contract testing
public partial class Program { }
