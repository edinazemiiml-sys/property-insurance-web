using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton<OpenAIPropertyAssistant>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", async (OpenAIPropertyAssistant ai) => Results.Ok(await ai.GetStatusAsync()));

app.MapPost("/api/chat", async (ChatRequest request, OpenAIPropertyAssistant ai) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Shkruaj një pyetje." });

    try
    {
        var answer = await ai.AskAsync(request.Message.Trim());
        return Results.Ok(new { answer });
    }
    catch (Exception ex)
    {
        return Results.Problem("Nuk u arrit të merret përgjigjja: " + ex.Message);
    }
});

app.Run();

public record ChatRequest(string Message);

public sealed class OpenAIPropertyAssistant
{
    private readonly HttpClient _http = new();
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private string? _vectorStoreId;

    private const string SystemInstructions = """
JE ASISTENT INFORMATIV PËR SIGURIMIN E PRONËS.

Qëllimi yt është t'u japësh njerëzve informacion PARA se të blejnë sigurimin e pronës. Nuk kontrollon polici individuale, nuk kontrollon nëse klienti ka blerë ndonjë mbulim dhe nuk duhet të kërkosh numër police.

BURIMI KONTRAKTUAL:
- Dokumenti i ngarkuar 'Kushtet e pergjithshme per sigurimin e prones.pdf' është burimi kryesor dhe autoriteti për përshkrimin e mbulimeve, klauzolave dhe përjashtimeve.
- Përdor file search për të gjetur pjesët relevante të dokumentit.
- Mos shpik nene, klauzola, kufij, përjashtime ose formulime që nuk gjenden në dokument.
- Nëse dokumenti nuk e mbështet një përgjigje, thuaj qartë se dokumenti nuk jep bazë të mjaftueshme për ta konfirmuar.

ANALIZA:
- Mund të përdorësh njohuri profesionale dhe praktikë ndërkombëtare të sigurimeve vetëm për të shpjeguar konceptin, terminologjinë, shkakun e dëmit ose pyetjet që duhet të sqarohen.
- Praktika ndërkombëtare NUK mund të krijojë, zgjerojë ose supozojë mbulim që nuk është në dokument.
- Mos thuaj 'mbulohet' vetëm sepse një rrezik është i zakonshëm në sigurime.
- Kur pyetja është për një rast konkret, analizo shkakun e dëmit, objektin, mënyrën e ngjarjes dhe përjashtimet relevante.
- Mbaj parasysh se dokumenti mund të përmbajë klauzola që duhen zgjedhur/specifikuar në policë; për këtë arsye, kur nuk ka polici konkrete, shpjego çfarë parashikon kushti dhe se mbulimi konkret varet nga përzgjedhja/kontraktimi përkatës.

FORMATI I PËRGJIGJES:
1. Përgjigjja e shkurtër.
2. Shpjegimi praktik.
3. Baza në Kushtet e Përgjithshme: përmend klauzolën/nënkapitullin dhe faqen vetëm kur mund ta mbështesësh nga dokumenti.
4. Përjashtimet ose kufizimet relevante.
5. Nëse mungon ndonjë fakt, bëj pyetje konkrete për ta sqaruar.

Shkruaj në shqip të qartë, profesionalisht, pa gjuhë të panevojshme juridike. Ky është informacion orientues para sigurimit dhe nuk zëvendëson ofertën, policën ose interpretimin përfundimtar kontraktual të siguruesit.
""";

    public OpenAIPropertyAssistant(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    private string? ApiKey => _config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private string Model => _config["OPENAI_MODEL"] ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-6-luna";

    private void Configure()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("API key nuk është konfiguruar në server. Vendose OPENAI_API_KEY në hosting/server.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
    }

    public async Task<object> GetStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            return new { ready = false, message = "Serveri nuk e ka OPENAI_API_KEY." };
        try
        {
            await EnsureVectorStoreAsync();
            return new { ready = true, message = "Kushtet e Përgjithshme janë gati për chat." };
        }
        catch (Exception ex)
        {
            return new { ready = false, message = ex.Message };
        }
    }

    private async Task<string> EnsureVectorStoreAsync()
    {
        if (!string.IsNullOrWhiteSpace(_vectorStoreId)) return _vectorStoreId;
        await _initLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_vectorStoreId)) return _vectorStoreId;
            Configure();

            var configuredVectorStore = _config["OPENAI_VECTOR_STORE_ID"] ?? Environment.GetEnvironmentVariable("OPENAI_VECTOR_STORE_ID");
            if (!string.IsNullOrWhiteSpace(configuredVectorStore))
            {
                _vectorStoreId = configuredVectorStore.Trim();
                return _vectorStoreId;
            }

            var saved = Path.Combine(_env.ContentRootPath, "Data", "vectorstore.id");
            if (File.Exists(saved))
            {
                var id = (await File.ReadAllTextAsync(saved)).Trim();
                if (!string.IsNullOrWhiteSpace(id)) { _vectorStoreId = id; return id; }
            }

            var pdfPath = Path.Combine(_env.ContentRootPath, "Data", "Kushtet e pergjithshme per sigurimin e prones.pdf");
            if (!File.Exists(pdfPath)) throw new FileNotFoundException("PDF-ja e Kushteve të Përgjithshme mungon në Data.");

            using var fileContent = new MultipartFormDataContent();
            fileContent.Add(new StringContent("assistants"), "purpose");
            var stream = File.OpenRead(pdfPath);
            var pdf = new StreamContent(stream);
            pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            fileContent.Add(pdf, "file", Path.GetFileName(pdfPath));
            var upload = await _http.PostAsync("https://api.openai.com/v1/files", fileContent);
            var uploadText = await upload.Content.ReadAsStringAsync();
            upload.EnsureSuccessStatusCode();
            var uploadJson = JsonDocument.Parse(uploadText);
            var fileId = uploadJson.RootElement.GetProperty("id").GetString()!;

            var vsBody = new { name = "Kushtet e Përgjithshme - Sigurimi i Pronës" };
            var vsResponse = await _http.PostAsync("https://api.openai.com/v1/vector_stores", Json(vsBody));
            var vsText = await vsResponse.Content.ReadAsStringAsync();
            vsResponse.EnsureSuccessStatusCode();
            var vsJson = JsonDocument.Parse(vsText);
            var vsId = vsJson.RootElement.GetProperty("id").GetString()!;

            var linkBody = new { file_id = fileId };
            var linkResponse = await _http.PostAsync($"https://api.openai.com/v1/vector_stores/{vsId}/files", Json(linkBody));
            linkResponse.EnsureSuccessStatusCode();

            for (var i = 0; i < 60; i++)
            {
                await Task.Delay(1000);
                var statusResponse = await _http.GetAsync($"https://api.openai.com/v1/vector_stores/{vsId}/files/{fileId}");
                var statusText = await statusResponse.Content.ReadAsStringAsync();
                statusResponse.EnsureSuccessStatusCode();
                var statusJson = JsonDocument.Parse(statusText);
                var status = statusJson.RootElement.GetProperty("status").GetString();
                if (status == "completed") break;
                if (status == "failed" || status == "cancelled") throw new InvalidOperationException("PDF-ja nuk u përgatit për kërkim.");
                if (i == 59) throw new TimeoutException("Përgatitja e PDF-së po zgjat më shumë se zakonisht.");
            }

            try { await File.WriteAllTextAsync(saved, vsId); } catch { }
            _vectorStoreId = vsId;
            return vsId;
        }
        finally { _initLock.Release(); }
    }

    public async Task<string> AskAsync(string question)
    {
        Configure();
        var vectorStore = await EnsureVectorStoreAsync();
        var body = new
        {
            model = Model,
            instructions = SystemInstructions,
            input = question,
            tools = new[] { new { type = "file_search", vector_store_ids = new[] { vectorStore } } },
            include = new[] { "file_search_call.results" }
        };

        var response = await _http.PostAsync("https://api.openai.com/v1/responses", Json(body));
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(text);

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("output_text", out var outputText))
            return outputText.GetString() ?? "Nuk u mor përgjigje.";

        foreach (var item in doc.RootElement.GetProperty("output").EnumerateArray())
        {
            if (item.TryGetProperty("content", out var content))
                foreach (var part in content.EnumerateArray())
                    if (part.TryGetProperty("text", out var t)) return t.GetString() ?? "Nuk u mor përgjigje.";
        }
        return "Nuk u mor përgjigje nga asistenti.";
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
