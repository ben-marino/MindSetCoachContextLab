using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;
using MindSetCoach.Api.Services.AI.Experiments;
using System.Text.Json;

namespace MindSetCoach.Api.Controllers;

/// <summary>
/// Controller for managing context engineering experiments.
/// Provides endpoints for running experiments, viewing results, and streaming progress.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExperimentsController : ControllerBase
{
    private readonly IExperimentRunnerService _experimentRunner;
    private readonly ContextExperimentLogger _experimentLogger;
    private readonly ExperimentsDbContext _dbContext;
    private readonly ILogger<ExperimentsController> _logger;

    public ExperimentsController(
        IExperimentRunnerService experimentRunner,
        ContextExperimentLogger experimentLogger,
        ExperimentsDbContext dbContext,
        ILogger<ExperimentsController> logger)
    {
        _experimentRunner = experimentRunner;
        _experimentLogger = experimentLogger;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Start a new experiment run. Returns immediately with run ID.
    /// The experiment executes in the background.
    /// </summary>
    /// <param name="request">Experiment configuration</param>
    /// <returns>Run ID and status</returns>
    [HttpPost("run")]
    [ProducesResponseType(typeof(StartExperimentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StartExperimentResponse>> RunExperiment([FromBody] RunExperimentRequest request)
    {
        // Validate experiment type
        var validTypes = new[] { "position", "persona", "compression" };
        if (!validTypes.Contains(request.ExperimentType.ToLower()))
        {
            return BadRequest(new { error = "ExperimentType must be 'position', 'persona', or 'compression'" });
        }

        if (string.IsNullOrEmpty(request.Provider) || string.IsNullOrEmpty(request.Model))
        {
            return BadRequest(new { error = "Provider and Model are required" });
        }

        try
        {
            var runId = await _experimentRunner.StartExperimentAsync(request);

            _logger.LogInformation(
                "Experiment started: RunId={RunId}, Type={Type}, Athlete={AthleteId}, Provider={Provider}, Model={Model}",
                runId, request.ExperimentType, request.AthleteId, request.Provider, request.Model);

            return Accepted(new StartExperimentResponse
            {
                RunId = runId,
                Status = "running",
                Message = $"Experiment started. Use GET /api/experiments/runs/{runId}/stream to monitor progress."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start experiment for athlete {AthleteId}", request.AthleteId);
            return StatusCode(500, new { error = "Failed to start experiment", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a list of experiment runs with optional filtering.
    /// </summary>
    /// <param name="provider">Filter by AI provider</param>
    /// <param name="model">Filter by model name</param>
    /// <param name="athleteId">Filter by athlete ID</param>
    /// <param name="experimentType">Filter by experiment type</param>
    /// <param name="limit">Maximum number of results (default: 20)</param>
    /// <returns>List of experiment runs with summary stats</returns>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(List<ExperimentRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExperimentRunDto>>> GetRuns(
        [FromQuery] string? provider = null,
        [FromQuery] string? model = null,
        [FromQuery] int? athleteId = null,
        [FromQuery] string? experimentType = null,
        [FromQuery] int limit = 20)
    {
        var query = _dbContext.ExperimentRuns
            .Include(r => r.Claims)
            .Include(r => r.PositionTests)
            .Where(r => !r.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(provider))
            query = query.Where(r => r.Provider.ToLower() == provider.ToLower());

        if (!string.IsNullOrEmpty(model))
            query = query.Where(r => r.Model.ToLower().Contains(model.ToLower()));

        if (athleteId.HasValue)
            query = query.Where(r => r.AthleteId == athleteId.Value);

        if (!string.IsNullOrEmpty(experimentType))
        {
            var type = experimentType.ToLower() switch
            {
                "position" => ExperimentType.Position,
                "compression" => ExperimentType.Compression,
                "persona" => ExperimentType.Persona,
                _ => (ExperimentType?)null
            };

            if (type.HasValue)
                query = query.Where(r => r.ExperimentType == type.Value);
        }

        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = runs.Select(r => r.ToDto()).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get full details for a specific experiment run including claims and receipts.
    /// </summary>
    /// <param name="runId">The experiment run ID</param>
    /// <returns>Full run details formatted for the HTML viewer</returns>
    [HttpGet("runs/{runId}")]
    [ProducesResponseType(typeof(ExperimentRunDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExperimentRunDetailDto>> GetRun(int runId)
    {
        var run = await _dbContext.ExperimentRuns
            .Include(r => r.Claims)
                .ThenInclude(c => c.Receipts)
            .Include(r => r.PositionTests)
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted);

        if (run == null)
        {
            return NotFound(new { error = $"Experiment run {runId} not found" });
        }

        return Ok(run.ToDetailDto());
    }

    /// <summary>
    /// Stream live progress updates for a running experiment using Server-Sent Events.
    /// </summary>
    /// <param name="runId">The experiment run ID</param>
    /// <returns>SSE stream of progress events</returns>
    [HttpGet("runs/{runId}/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task StreamProgress(int runId)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var channel = _experimentRunner.GetProgressChannel(runId);

        if (channel == null)
        {
            // Check if the run exists and is complete
            var run = await _dbContext.ExperimentRuns.FindAsync(runId);
            if (run == null)
            {
                await WriteSSEEvent("error", new { message = $"Experiment run {runId} not found" });
                return;
            }

            if (run.Status == ExperimentStatus.Completed)
            {
                await WriteSSEEvent("complete", new { message = "Experiment already completed", runId });
                return;
            }

            if (run.Status == ExperimentStatus.Failed)
            {
                await WriteSSEEvent("error", new { message = "Experiment failed" });
                return;
            }

            // Run is pending but not started yet
            await WriteSSEEvent("info", new { message = "Experiment is pending" });
            return;
        }

        try
        {
            await foreach (var progressEvent in channel.Reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                await WriteSSEEvent(progressEvent.Type, new
                {
                    message = progressEvent.Message,
                    data = progressEvent.Data,
                    timestamp = progressEvent.Timestamp
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
            _logger.LogInformation("Client disconnected from SSE stream for run {RunId}", runId);
        }
    }

    private async Task WriteSSEEvent(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await Response.WriteAsync($"event: {eventType}\n");
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    /// <summary>
    /// Soft delete an experiment run.
    /// </summary>
    /// <param name="runId">The experiment run ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("runs/{runId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRun(int runId)
    {
        var run = await _dbContext.ExperimentRuns.FindAsync(runId);

        if (run == null)
        {
            return NotFound(new { error = $"Experiment run {runId} not found" });
        }

        if (run.IsDeleted)
        {
            return NotFound(new { error = $"Experiment run {runId} not found" });
        }

        // Check if experiment is still running
        if (_experimentRunner.IsRunning(runId))
        {
            return BadRequest(new { error = "Cannot delete a running experiment" });
        }

        run.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Soft deleted experiment run {RunId}", runId);

        return NoContent();
    }

    /// <summary>
    /// Get experiment statistics and summary.
    /// </summary>
    /// <returns>Aggregated experiment statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ExperimentSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExperimentSummary>> GetStats()
    {
        var summary = await _experimentLogger.GetSummaryAsync();
        return Ok(summary);
    }
}
