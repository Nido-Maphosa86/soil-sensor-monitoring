// What this file is for: a background job that runs the alert evaluator every 10 seconds inside the web app, so alerts appear almost at once.
// How it fits in: Web project hosted service. It runs when the setting SensorGrid:RunAlertEvaluatorInWebApp is true (the default: on the free Firebase plan
// it is the only alert checker). When Firebase Cloud Functions run the evaluator instead, set it to false. The evaluator never raises a duplicate for an alert that is still open.
using SensorGrid.Service;

namespace SensorGrid.Web.Filters;

public sealed class AlertEvaluatorWorker : BackgroundService
{
    /// <summary>How often the check runs. Each run reads only the newest reading of each sensor, so it is cheap.</summary>
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    private readonly IAlertEvaluator _evaluator;
    private readonly ILogger<AlertEvaluatorWorker> _log;

    public AlertEvaluatorWorker(IAlertEvaluator evaluator, ILogger<AlertEvaluatorWorker> log)
    {
        _evaluator = evaluator;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        do
        {
            try
            {
                var raised = await _evaluator.EvaluateAsync();
                if (raised > 0) _log.LogInformation("Raised {Count} new alert(s).", raised);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Alert check failed; it will be tried again in 2 seconds.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
