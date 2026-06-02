using DnnDeploymentAgent.Agents;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// OpenAI Configuration

var apiKey = builder.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "OpenAI:ApiKey is missing.");
}

// Semantic Kernel

var kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.AddOpenAIChatCompletion(
    modelId: "gpt-5",
    apiKey: apiKey);

var kernel = kernelBuilder.Build();

builder.Services.AddSingleton(kernel);

// Agents

builder.Services.AddSingleton<GitAgent>();

// Future agents
builder.Services.AddSingleton<SqlAgent>();
builder.Services.AddSingleton<IisAgent>();
builder.Services.AddSingleton<ManagerAgent>();

builder.Services.AddHttpClient();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// Logging

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Dnn Deployment Agent");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();