using Common;
using Grpc.Core;

namespace Broker.Tests
{
     public class RetryPolicyTests
     {
          [Theory]
          [InlineData(1, 50, 100)]
          [InlineData(2, 100, 200)]
          [InlineData(3, 200, 400)]
          [InlineData(10, 500, 1000)]
          public void Delay_grows_exponentially_with_jitter_and_is_capped(int attempt, double minMs, double maxMs)
          {
               var random = new Random(42);
               for (var i = 0; i < 100; i++)
               {
                    var delay = RetryPolicy.GetDelay(attempt, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), random);
                    Assert.InRange(delay.TotalMilliseconds, minMs, maxMs);
               }
          }

          [Theory]
          [InlineData(StatusCode.Unavailable, true)]
          [InlineData(StatusCode.DeadlineExceeded, true)]
          [InlineData(StatusCode.InvalidArgument, false)]
          [InlineData(StatusCode.FailedPrecondition, false)]
          [InlineData(StatusCode.AlreadyExists, false)]
          public void Only_transient_errors_are_retried(StatusCode code, bool expected)
          {
               Assert.Equal(expected, RetryPolicy.IsTransient(code));
          }
     }
}
