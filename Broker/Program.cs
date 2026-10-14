
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
               AppContext.SetSwitch(
                   "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
                   true);

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
