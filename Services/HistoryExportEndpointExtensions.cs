using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public static class HistoryExportEndpointExtensions
{
    public static RouteHandlerBuilder MapHistoryExport(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<IHistoryExportService, HistoryQuery, CancellationToken, Task<ExportFile>> createExport) =>
        endpoints.MapGet(pattern, async ([AsParameters] HistoryExportRequest request, IHistoryExportService historyExportService, IOptions<ExportOptions> exportOptions, ILogger<ExportFileResult> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await createExport(historyExportService, request.ToHistoryQuery(), cancellationToken);
                return new ExportFileResult(file, exportOptions.Value, logger);
            }
            catch (HistoryExportLimitExceededException exception)
            {
                return ExportLimitProblem(exception);
            }
        })
        .RequireAuthorization(Policies.AdminAccess)
        .RequireRateLimiting("exports");

    public static RouteHandlerBuilder MapTrendPdfExport(this IEndpointRouteBuilder endpoints, string pattern) =>
        endpoints.MapGet(pattern, async (int id, ITrendExportService trendExportService, IOptions<ExportOptions> exportOptions, ILogger<ExportFileResult> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await trendExportService.CreatePdfAsync(id, cancellationToken);
                return file is null ? Results.NotFound() : new ExportFileResult(file, exportOptions.Value, logger);
            }
            catch (TrendExportLimitExceededException exception)
            {
                return ExportLimitProblem(exception);
            }
        })
        .RequireRateLimiting("exports");

    private static IResult ExportLimitProblem(Exception exception) =>
        Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Export limit exceeded", detail: exception.Message);
}