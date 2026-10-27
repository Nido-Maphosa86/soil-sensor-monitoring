// What this file is for: a background job that runs the alert evaluator once a minute inside the web app.
// How it fits in: Web project hosted service. It only runs when the setting SensorGrid:RunAlertEvaluatorInWebApp is true (the fallback for the free Firebase plan).
// When Firebase Cloud Functions run the evaluator instead, leave the setting false. The evaluator never raises a duplicate for an alert that is still open.
using SensorGrid.Service;

namespace SensorGrid.Web.Filters;

public sealed class AlertEvaluatorWorker : BackgroundService
{
    private readonly IAlertEvaluator _evaluator;
    private readonly ILogger<AlertEvaluatorWorker> _log;

    public AlertEvaluatorWorker(IAlertEvaluator evaluator, ILogger<AlertEvaluatorWorker> log)
    {
        _evaluator = evaluator;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                var raised = await _evaluator.EvaluateAsync();
                if (raised > 0) _log.LogInformation("Raised {Count} new alert(s).", raised);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Alert check failed; it will be tried again in a minute.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
