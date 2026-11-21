using System.Text;
using GroupDocs.Assembly;
using GroupDocs.Assembly.Data;

namespace ReportService;

/// <summary>
/// The two things the service does: assemble a report from JSON, and render a page of it.
/// The calls mirror the official examples repository
/// (GroupDocs.Assembly-for-.NET/Examples/.../AdvancedUsage/GenerateDocumentPreview.cs).
/// </summary>
public sealed class ReportBuilder
{
    private readonly ILogger<ReportBuilder> _logger;

    public ReportBuilder(ILogger<ReportBuilder> logger)
    {
        _logger = logger;
        ApplyLicense();
    }

    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// Assembles the report in memory and hands back the bytes, which is what a web request needs:
    /// nothing is written to disk, so two requests never fight over the same file.
    /// </summary>
    public byte[] Assemble(string json, bool asPdf)
    {
        using MemoryStream data = new(Encoding.UTF8.GetBytes(json));
        using MemoryStream report = new();

        // Template and report are both streams here: a web request has no business writing
        // intermediate files, and the stream overloads keep the whole thing in memory.
        using FileStream template = File.OpenRead(RepositoryPaths.Template("Customer orders.docx"));

        DocumentAssembler assembler = new();

        if (asPdf)
        {
            assembler.AssembleDocument(
                template,
                report,
                new LoadSaveOptions(FileFormat.Pdf),
                new DataSourceInfo(new JsonDataSource(data), "customers"));
        }
        else
        {
            assembler.AssembleDocument(
                template,
                report,
                new DataSourceInfo(new JsonDataSource(data), "customers"));
        }

        _logger.LogInformation("Assembled a {Format} report of {Bytes} bytes", asPdf ? "PDF" : "DOCX", report.Length);
        return report.ToArray();
    }

    /// <summary>
    /// Renders one page of an assembled report to PNG. Supported since 26.11.
    ///
    /// This is what lets the browser show the report before the reader downloads it, without
    /// embedding a viewer and without converting the whole document to HTML.
    /// </summary>
    public byte[] PreviewPage(byte[] document, int pageNumber, int resolution)
    {
        using MemoryStream source = new(document);
        MemoryStream? page = null;

        PreviewOptions previewOptions = new(
            _ => page = new MemoryStream(),
            (_, _) => { /* keep the stream: it is read after GeneratePreview returns */ })
        {
            PreviewFormat = PreviewFormats.PNG,
            PageNumbers = new[] { pageNumber },   // 1-based
            Resolution = resolution
        };

        new DocumentAssembler().GeneratePreview(source, previewOptions);

        if (page is null)
        {
            throw new InvalidOperationException($"The document has no page {pageNumber}.");
        }

        return page.ToArray();
    }

    public string SampleJson() => File.ReadAllText(RepositoryPaths.DataFile("Customers.json"));

    /// <summary>Optional: without a licence the output carries evaluation marks.</summary>
    private void ApplyLicense()
    {
        string? path = Environment.GetEnvironmentVariable("GROUPDOCS_ASSEMBLY_LICENSE")?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            new License().SetLicense(path);
            _logger.LogInformation("GroupDocs.Assembly licence applied");
        }
        else
        {
            _logger.LogWarning("GROUPDOCS_ASSEMBLY_LICENSE points at a missing file: {Path}", path);
        }
    }
}
