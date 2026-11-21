using ReportService;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ReportBuilder>();

WebApplication app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// The data the sample page starts from, so the browser has something to send back.
app.MapGet("/api/sample", (ReportBuilder reports) => Results.Text(reports.SampleJson(), "application/json"));

// POST the data, get the file. Content-Disposition makes the browser download it.
app.MapPost("/api/reports", async (HttpRequest request, ReportBuilder reports, string? format) =>
{
    using StreamReader reader = new(request.Body);
    string json = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(json))
    {
        return Results.BadRequest(new { error = "The request body is empty." });
    }

    bool asPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    try
    {
        byte[] report = reports.Assemble(json, asPdf);

        return Results.File(
            report,
            asPdf ? "application/pdf" : ReportBuilder.DocxContentType,
            asPdf ? "customer-orders.pdf" : "customer-orders.docx");
    }
    catch (Exception exception)
    {
        // A template or data error is the caller's problem, not a server fault.
        return Results.BadRequest(new { error = exception.Message });
    }
});

// The same report rendered to an image, so the page can show it before anyone downloads it.
app.MapPost("/api/reports/preview", async (HttpRequest request, ReportBuilder reports, int? page, int? dpi) =>
{
    using StreamReader reader = new(request.Body);
    string json = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(json))
    {
        return Results.BadRequest(new { error = "The request body is empty." });
    }

    try
    {
        byte[] report = reports.Assemble(json, asPdf: false);
        byte[] image = reports.PreviewPage(report, page ?? 1, dpi ?? 120);
        return Results.File(image, "image/png");
    }
    catch (Exception exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.Run();
