using GillyTracker.Core.Auth;
using GillyTracker.Core.Sightings;
using GillyTracker.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace GillyTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SightingsController(ApplicationDbContext dbContext, ILogger<SightingsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AdminAccessSettings.PolicyName)]
    public async Task<IActionResult> GetReports(CancellationToken cancellationToken)
    {
        try
        {
            var reports = await dbContext.DogSightingReports
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new SightingResponse(
                    x.Id,
                    x.Latitude,
                    x.Longitude,
                    x.ReporterDetails,
                    x.CreatedDate))
                .ToListAsync(cancellationToken);

            return Ok(reports);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Failed to query sightings list. Returning an empty list.");
            return Ok(Array.Empty<SightingResponse>());
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] CreateSightingRequest request, CancellationToken cancellationToken)
    {
        if (request.Details?.Length > 2000)
        {
            ModelState.AddModelError(nameof(CreateSightingRequest.Details), "Details must be 2000 characters or fewer.");
            return ValidationProblem(ModelState);
        }

        if (!CoordinateValidator.IsValid(request.Latitude, request.Longitude))
        {
            ModelState.AddModelError(nameof(CreateSightingRequest.Latitude), "Latitude must be between -90 and 90.");
            ModelState.AddModelError(nameof(CreateSightingRequest.Longitude), "Longitude must be between -180 and 180.");
            return ValidationProblem(ModelState);
        }

        var report = new DogSightingReport
        {
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ReporterDetails = request.Details?.Trim(),
            CreatedDate = DateTimeOffset.UtcNow
        };

        dbContext.DogSightingReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetReport), new { id = report.Id }, new SightingResponse(
            report.Id,
            report.Latitude,
            report.Longitude,
            report.ReporterDetails,
            report.CreatedDate));
    }

    [HttpPost("import")]
    [Authorize(Policy = AdminAccessSettings.PolicyName)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ImportReports(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "A CSV file is required.");
            return ValidationProblem(ModelState);
        }

        SightingCsvParseResult parseResult;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        {
            parseResult = SightingCsvParser.Parse(reader);
        }

        var reports = parseResult.Rows
            .Select(row => new DogSightingReport
            {
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                ReporterDetails = row.Details,
                CreatedDate = row.CreatedDate
            })
            .ToList();

        if (reports.Count > 0)
        {
            dbContext.DogSightingReports.AddRange(reports);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new ImportSightingsResponse(
            reports.Count,
            parseResult.Errors.Select(e => new ImportSightingsRowError(e.LineNumber, e.Message)).ToList()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AdminAccessSettings.PolicyName)]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken cancellationToken)
    {
        var report = await dbContext.DogSightingReports
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SightingResponse(
                x.Id,
                x.Latitude,
                x.Longitude,
                x.ReporterDetails,
                x.CreatedDate))
            .SingleOrDefaultAsync(cancellationToken);

        return report is null ? NotFound() : Ok(report);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AdminAccessSettings.PolicyName)]
    public async Task<IActionResult> DeleteReport(Guid id, CancellationToken cancellationToken)
    {
        var report = await dbContext.DogSightingReports
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        dbContext.DogSightingReports.Remove(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public record CreateSightingRequest(decimal Latitude, decimal Longitude, string? Details);

public record SightingResponse(Guid Id, decimal Latitude, decimal Longitude, string? Details, DateTimeOffset CreatedDate);

public record ImportSightingsResponse(int ImportedCount, IReadOnlyList<ImportSightingsRowError> Errors);

public record ImportSightingsRowError(int LineNumber, string Message);
