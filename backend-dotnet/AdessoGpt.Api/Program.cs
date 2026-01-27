using DotNetEnv;
using Google.GenAI;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// .env laden (sucht in verschiedenen Pfaden)
static void LoadEnv()
{
    var cwd = Directory.GetCurrentDirectory();
    var candidates = new[]
    {
        Path.Combine(cwd, ".env"),
        Path.Combine(cwd, "..", ".env"),
        Path.Combine(cwd, "..", "..", ".env"),
    };
    var path = candidates.FirstOrDefault(File.Exists);
    if (path != null) Env.Load(path);
}

LoadEnv();

var builder = WebApplication.CreateBuilder(args);

var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
              ?? throw new Exception("GEMINI_API_KEY fehlt in .env");

var mcpUrl = Environment.GetEnvironmentVariable("MCP_URL")
           ?? "http://localhost:8000/mcp";

// CORS für Frontend
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()
));

// MCP Client registrieren
builder.Services.AddHttpClient<McpHttpClient>();
builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(McpHttpClient));
    return new McpHttpClient(http, mcpUrl);
});

// Gemini ChatClient mit Function Calling
builder.Services.AddSingleton<IChatClient>(_ =>
{
    var client = new Client(apiKey: geminiKey);
    return client.AsIChatClient("gemini-2.5-flash-lite")
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
});

var app = builder.Build();
app.UseCors();

// MCP Session initialisieren
await app.Services.GetRequiredService<McpHttpClient>().InitializeAsync();

// Chat-Endpoint
app.MapPost("/api/chat", async (ChatIn input, IChatClient chat, McpHttpClient mcp, CancellationToken ct) =>
{
    // Tools definieren: Gemini kann diese aufrufen, Ausführung erfolgt via MCP
    var options = new ChatOptions
    {
        Tools =
        [
            AIFunctionFactory.Create(
                async ([Description("Erste ganze Zahl")] int a,
                       [Description("Zweite ganze Zahl")] int b) 
                    => await mcp.AddAsync(a, b, ct),
                "add",
                "Addiert zwei Zahlen"
            ),
            AIFunctionFactory.Create(
                async ([Description("IANA Zeitzone, z.B. Europe/Berlin")] string tz) 
                    => await mcp.GetTimeAsync(tz, ct),
                "get_time",
                "Gibt die aktuelle Uhrzeit zurück"
            )
        ]
    };

    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, "Nutze die Tools add und get_time wenn nötig. Antworte immer mit Text."),
        new(ChatRole.User, input.message)
    };

    var resp = await chat.GetResponseAsync(messages, options, ct);

    // Antwort extrahieren (mit Fallback falls Tool-Aufruf keine direkte Textantwort liefert)
    var reply = resp.Text;
    
    if (string.IsNullOrWhiteSpace(reply))
    {
        // Fallback: Letzte Assistant-Nachricht mit Text suchen
        reply = resp.Messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
            .LastOrDefault()?.Text;
    }

    if (string.IsNullOrWhiteSpace(reply))
    {
        // Fallback: Bei Tool-Ergebnis ohne Textantwort nochmal nachfragen
        var hasToolResult = resp.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any();
        if (hasToolResult)
        {
            var followUp = new List<ChatMessage>(messages);
            followUp.AddRange(resp.Messages);
            followUp.Add(new ChatMessage(ChatRole.User, "Antworte basierend auf dem Tool-Ergebnis."));
            
            var followUpResp = await chat.GetResponseAsync(followUp, options, ct);
            reply = followUpResp.Text;
        }
    }

    return Results.Ok(new ChatOut(reply ?? "Keine Antwort erhalten."));
});

app.Run();

record ChatIn(string message);
record ChatOut(string reply);
