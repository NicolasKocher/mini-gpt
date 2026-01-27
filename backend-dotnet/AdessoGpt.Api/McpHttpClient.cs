using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class McpHttpClient
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private string? _sessionId;
    private string _protocolVersion = "2025-06-18";
    private int _id = 1;

    public McpHttpClient(HttpClient http, string endpoint)
    {
        _http = http;
        _endpoint = endpoint.TrimEnd('/');
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream"); // Spec verlangt Accept für beides
    }

    /// <summary>
    /// Extrahiert JSON aus einer SSE-Antwort (text/event-stream) oder gibt den String direkt zurück wenn es JSON ist.
    /// </summary>
    private static string ExtractJsonFromSse(string response)
    {
        // Wenn es bereits gültiges JSON ist (beginnt mit {), direkt zurückgeben
        var trimmed = response.TrimStart();
        if (trimmed.StartsWith('{'))
            return trimmed;

        // SSE-Format: "event: message\ndata: {...}\n\n"
        // Wir suchen nach der "data:" Zeile und extrahieren das JSON
        var match = Regex.Match(response, @"^data:\s*(.+)$", RegexOptions.Multiline);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        throw new InvalidOperationException($"Konnte kein JSON aus SSE-Antwort extrahieren: {response[..Math.Min(200, response.Length)]}");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var initReq = new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = _protocolVersion,
                capabilities = new { },
                clientInfo = new { name = "adesso-gpt-backend", version = "0.1.0" }
            }
        };

        using var resp = await PostAsync(initReq, ct);

        if (resp.Headers.TryGetValues("Mcp-Session-Id", out var values))
            _sessionId = values.FirstOrDefault();

        var rawResponse = await resp.Content.ReadAsStringAsync(ct);
        var initJson = ExtractJsonFromSse(rawResponse);

        using var doc = JsonDocument.Parse(initJson);
        var negotiated = doc.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString();
        if (!string.IsNullOrWhiteSpace(negotiated))
            _protocolVersion = negotiated;

        // notifications/initialized :contentReference[oaicite:7]{index=7}
        var notif = new { jsonrpc = "2.0", method = "notifications/initialized" };
        using var notifResp = await PostAsync(notif, ct);
        if ((int)notifResp.StatusCode != 202)
            throw new InvalidOperationException($"MCP initialized notification failed: {notifResp.StatusCode}");
    }

    public async Task<int> AddAsync(int a, int b, CancellationToken ct = default)
    {
        var req = new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new { name = "add", arguments = new { a, b } }
        };

        using var resp = await PostAsync(req, ct);
        var rawResponse = await resp.Content.ReadAsStringAsync(ct);
        var json = ExtractJsonFromSse(rawResponse);

        using var doc = JsonDocument.Parse(json);
        // FastMCP structured output: result.structuredContent.result
        var result = doc.RootElement.GetProperty("result");
        if (result.TryGetProperty("structuredContent", out var sc) && sc.TryGetProperty("result", out var r))
            return r.GetInt32();

        // Fallback: content[0].text
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        return int.Parse(text ?? "0");
    }

    public async Task<string> GetTimeAsync(string tz, CancellationToken ct = default)
    {
        var req = new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new { name = "get_time", arguments = new { tz } }
        };

        using var resp = await PostAsync(req, ct);
        var rawResponse = await resp.Content.ReadAsStringAsync(ct);
        var json = ExtractJsonFromSse(rawResponse);

        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("result");
        if (result.TryGetProperty("structuredContent", out var sc) && sc.TryGetProperty("result", out var r))
            return r.GetString() ?? "";

        return result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<HttpResponseMessage> PostAsync<T>(T body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(body)
        };

        // Session Management + Protocol Header :contentReference[oaicite:8]{index=8}
        if (!string.IsNullOrWhiteSpace(_sessionId))
            req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);

        req.Headers.TryAddWithoutValidation("MCP-Protocol-Version", _protocolVersion);

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return resp;
    }

    private int NextId() => Interlocked.Increment(ref _id);
}
