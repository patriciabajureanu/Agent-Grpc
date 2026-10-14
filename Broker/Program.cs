
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
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                        logging.AddFilter(
                         "Microsoft.AspNetCore",
                         LogLevel.Information);
                   })
                   .UseStartup<Startup>()
                   .UseUrls(EndpointsConstants.BrokerAddress)
                   .Build()
                   .Run();
          }
     }
}
