using Microsoft.Extensions.AI;
using OllamaSharp;
using phi4_weather_agent_dotnet_temp.Components;
using phi4_weather_agent_dotnet_temp.Services;
using phi4_weather_agent_dotnet_temp.Services.Ingestion;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Services;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Handlers;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Dispatching;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Configure AI settings with Options pattern
builder.Services.Configure<AIConfiguration>(builder.Configuration.GetSection("AI"));

// Register AI configuration services
builder.Services.AddSingleton<ConfigurationValidator>();
builder.Services.AddHostedService<ConfigurationValidator>();
builder.Services.AddSingleton<ConfigurationProvider>();
builder.Services.AddSingleton<IPromptProvider, PromptProvider>();
builder.Services.AddSingleton<ChatClientFactory>();

// Register functools dependencies
builder.Services.AddSingleton<IFunctoolsParser, FunctoolsParser>();
builder.Services.AddSingleton<IToolInvoker, ToolInvoker>();

// Register FunctoolsHandler as keyed service
builder.Services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandler>("Functools");

// Create IChatClient using factory
var chatClientFactory = builder.Services.BuildServiceProvider().GetRequiredService<ChatClientFactory>();
var chatClient = chatClientFactory.CreateChatClient();

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new OllamaApiClient(new Uri("http://localhost:11434"),
    "all-minilm");

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
