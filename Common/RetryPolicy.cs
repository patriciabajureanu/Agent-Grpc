using Grpc.Core;

namespace Common
{
     public static class RetryPolicy
     {
          // Backoff exponential cu "equal jitter": jumatate din intarziere este fixa, jumatate aleatoare,
          // ca mai multi clienti care au cazut odata sa nu reincerce toti in acelasi moment.
          public static TimeSpan GetDelay(int attempt, TimeSpan baseDelay, TimeSpan maxDelay, Random? random = null)
          {
               var exponential = Math.Min(maxDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
               return TimeSpan.FromMilliseconds(exponential / 2 + (random ?? Random.Shared).NextDouble() * exponential / 2);
          }

          // Doar erorile temporare se reincearca; erorile de validare ar esua identic la fiecare incercare.
          public static bool IsTransient(StatusCode code)
          {
               return code is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted;
          }
     }
}
