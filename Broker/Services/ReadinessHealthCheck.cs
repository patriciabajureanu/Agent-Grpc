using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Broker.Services
{
     // Nu face operatii costisitoare si nu modifica starea: citeste doar ce a observat PersistenceWorker.
     public class ReadinessHealthCheck : IHealthCheck
     {
          private readonly PersistenceWorker _persistence;

          public ReadinessHealthCheck(PersistenceWorker persistence)
          {
               _persistence = persistence;
          }

          public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
          {
               if (!_persistence.IsRestored)
               {
                    return Task.FromResult(HealthCheckResult.Unhealthy("Restoring the backup."));
               }

               // Broker-ul inca livreaza, dar stirile persistente noi nu ar supravietui unei reporniri.
               if (_persistence.LastBackupError is { } error)
               {
                    return Task.FromResult(HealthCheckResult.Degraded($"Backup storage unavailable: {error}"));
               }

               return Task.FromResult(HealthCheckResult.Healthy());
          }
     }
}
