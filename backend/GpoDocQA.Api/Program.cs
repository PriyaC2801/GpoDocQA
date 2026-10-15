using GpoDocQA.Api.Configuration;
using GpoDocQA.Api.Services;
using Microsoft.Extensions.AI;
using Mscc.GenerativeAI.Microsoft;

var builder = WebApplication.CreateBuilder(args);

const string ReactAppCorsPolicy = "ReactApp";

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Turns unhandled exceptions into RFC 7807 problem-details JSON responses
// instead of leaking stack traces to the client.
builder.Services.AddProblemDetails();

builder.Services.Configure<DocumentStorageOptions>(
    builder.Configuration.GetSection(DocumentStorageOptions.SectionName));

builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();
builder.Services.AddHostedService<StaticDocumentLoader>();
builder.Services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
builder.Services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();

var geminiOptions = builder.Configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>()
    ?? new GeminiOptions();

if (string.IsNullOrWhiteSpace(geminiOptions.ApiKey))
{
    throw new InvalidOperationException(
        "Gemini API key is missing. Run this in the backend project folder:\n" +
        "  dotnet user-secrets set \"Gemini:ApiKey\" \"YOUR_KEY_HERE\"");
}

builder.Services.AddSingleton(geminiOptions);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChatClient>(
    new GeminiChatClient(apiKey: geminiOptions.ApiKey, model: geminiOptions.ChatModel));
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddScoped<IChatService, ChatService>();

// The vector database: PostgreSQL with the pgvector extension for similarity
// search, wired up through Semantic Kernel's VectorData abstractions.
var vectorDbConnectionString = builder.Configuration.GetConnectionString("VectorDb");

if (string.IsNullOrWhiteSpace(vectorDbConnectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is missing. Run this in the backend project folder:\n" +
        "  dotnet user-secrets set \"ConnectionStrings:VectorDb\" \"Host=...;Database=...;Username=...;Password=...\"");
}

builder.Services.AddPostgresVectorStore(vectorDbConnectionString);
builder.Services.AddSingleton<IChunkVectorStore>(sp =>
    new PostgresChunkVectorStore(sp.GetRequiredService<Microsoft.Extensions.VectorData.VectorStore>(), vectorDbConnectionString));

// The React dev server (Vite) runs on a different origin, so the browser
// needs explicit permission to call this API from there.
builder.Services.AddCors(options =>
{
    options.AddPolicy(ReactAppCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(ReactAppCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
