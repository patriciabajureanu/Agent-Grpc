using Common;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Broker
{
     public class Program
     {
          public static void Main(string[] args)
          {
               WebHost.CreateDefaultBuilder(args)
                   .ConfigureLogging(logging =>
                   {
                        // O linie JSON per eveniment, cu timestamp UTC si campurile din scope (CorrelationId, MessageId).
                        logging.ClearProviders();
                        logging.AddJsonConsole(options =>
                        {
                             options.IncludeScopes = true;
                             options.UseUtcTimestamp = true;
                             options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                        });
                   })
                   .UseStartup<Startup>()
                   .UseUrls(EndpointsConstants.BrokerAddress)
                   .Build()
                   .Run();
          }
     }
}
