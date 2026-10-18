using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class ExportFileResult(
    ExportFile file,
    ExportOptions exportOptions,
    ILogger<ExportFileResult> logger) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (file.ContentLength is long contentLength)
        {
            SetDownloadHeaders(httpContext, contentLength);
            await file.WriteToAsync(httpContext.Response.Body, httpContext.RequestAborted);
            return;
        }

        string? temporaryPath = null;
        try
        {
            await using var temporaryFile = ExportTemporaryStorage.Create(exportOptions, out temporaryPath);
            await file.WriteToAsync(temporaryFile, httpContext.RequestAborted);
            temporaryFile.Position = 0;
            SetDownloadHeaders(httpContext, temporaryFile.Length);
            await temporaryFile.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to generate export {FileName}", file.FileName);
            throw;
        }
        finally
        {
            if (temporaryPath is not null) File.Delete(temporaryPath);
        }
    }

    private void SetDownloadHeaders(HttpContext httpContext, long contentLength)
    {
        var disposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = file.FileName };
        httpContext.Response.ContentType = file.ContentType;
        httpContext.Response.ContentLength = contentLength;
        httpContext.Response.Headers.ContentDisposition = disposition.ToString();
    }
}