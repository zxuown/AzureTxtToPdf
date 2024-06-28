using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Drawing.Layout;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AzureTxtToPdf.Controllers;
[Route("home")]
[ApiController]
public class HomeController : Controller
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<HomeController> _logger;
    private readonly QueueClient _queueClient;

    public HomeController(BlobServiceClient blobServiceClient, IConfiguration configuration, ILogger<HomeController> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _queueClient = new QueueClient(configuration.GetValue<string>("ConnectionStrings:BlobStorage"), "texttopdf");
    }
    [HttpPost]
    public async Task<ActionResult> IndexAsync(IFormFile? file)
    {
        if (file == null)
        {
            _logger.LogWarning("File upload failed: No file provided.");
            return BadRequest("No file provided.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient("text-files");
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(file.FileName);

        using (var stream = file.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, true);
        }
        try
        {
            await _queueClient.SendMessageAsync(file.FileName);
            await ProcessFileAsync(file.FileName);
            _logger.LogInformation($"File created: {file.FileName}", file.FileName);
            _logger.LogInformation($"File created: {Path.ChangeExtension(file.FileName, ".pdf")}", Path.ChangeExtension(file.FileName, ".pdf"));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
            return StatusCode(500, "Internal server error");

        }
    }
    private async Task ProcessFileAsync(string fileName)
    {
        var textBlobContainer = _blobServiceClient.GetBlobContainerClient("text-files");
        await textBlobContainer.CreateIfNotExistsAsync();
        var textBlobClient = textBlobContainer.GetBlobClient(fileName);

        var pdfBlobContainer = _blobServiceClient.GetBlobContainerClient("pdf-files");
        await pdfBlobContainer.CreateIfNotExistsAsync();
        var pdfBlobClient = pdfBlobContainer.GetBlobClient(Path.ChangeExtension(fileName, ".pdf"));

        using var memoryStream = new MemoryStream();
        await textBlobClient.DownloadToAsync(memoryStream);
        string textContent = Encoding.UTF8.GetString(memoryStream.ToArray());

        using var pdfStream = new MemoryStream();
        var document = new PdfDocument();
        var page = document.AddPage();
        var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Verdana", 12);
        var textFormatter = new XTextFormatter(graphics);

        var rect = new XRect(40, 40, page.Width - 80, page.Height - 80);
        graphics.DrawRectangle(XBrushes.Transparent, rect);

        textFormatter.DrawString(textContent, font, XBrushes.Black, rect, XStringFormats.TopLeft);

        document.Save(pdfStream, false);
        pdfStream.Position = 0;

        await pdfBlobClient.UploadAsync(pdfStream, true);
    }
}
