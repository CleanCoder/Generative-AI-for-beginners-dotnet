using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Azure.AI.Inference;
using Azure;
using Microsoft.Extensions.AI;
using MEAI.Services;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Mscc.GenerativeAI.Microsoft;
using MEAI.Controllers;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.LocalDebug.json", true, true);
#endif

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Embedding services
builder.Services.TryAddSingleton<IVectorStore, InMemoryVectorStore>();
builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>, AzureAIInferenceEmbeddingGenerator>("AzureAI", (serviceProvider, key) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<AzureAIInferenceEmbeddingGenerator>>();
    logger.LogInformation("Creating AzureAIInferenceEmbeddingGenerator");

    var url = builder.Configuration.GetValue<string>("AzureAI:Host");
    var token = builder.Configuration.GetValue<string>("AzureAI:Token");

    var client = new EmbeddingsClient(new Uri(url), new AzureKeyCredential(token), new AzureAIInferenceClientOptions());

    return new AzureAIInferenceEmbeddingGenerator(client);
});
builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>, OllamaEmbeddingGenerator>("Ollama", (serviceProvider, key) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<OllamaEmbeddingGenerator>>();
    logger.LogInformation("Creating OllamaEmbeddingGenerator");

    var url = builder.Configuration.GetValue<string>("Ollama:Host");
    var model = builder.Configuration.GetValue<string>("Ollama:Model");

    return new OllamaEmbeddingGenerator(new Uri(url), model);
});
builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>, GeminiEmbeddingGenerator>("Gemini", (serviceProvider, key) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<GeminiEmbeddingGenerator>>();
    logger.LogInformation("Creating GeminiEmbeddingGenerator");

    var token = builder.Configuration.GetValue<string>("Gemini:Token");
    var model = builder.Configuration.GetValue<string>("Gemini:Model");

    return new GeminiEmbeddingGenerator(token, model);
});


builder.Services.AddScoped<IMovieSearchService<int>, AIMovieSearchService<int>>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.IncludeXmlComments(typeof(MoviesController).Assembly);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "My API V1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "Hello world!");

app.Run();
