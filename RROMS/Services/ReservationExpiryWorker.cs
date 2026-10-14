// Background timer: periodically expires unpaid payment holds and marks overdue confirmed reservations as no-shows through ReservationService.
namespace RROMS.Services;

public class ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReservationService>().Expire();
            }
            catch (Exception ex) { logger.LogError(ex, "Reservation expiry processing failed; will retry."); }
        }
    }
}
