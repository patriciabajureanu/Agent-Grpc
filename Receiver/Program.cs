
using Common;
using Grpc.Net.Client;
using GrpcAgent;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

namespace Receiver
{
     public class Program
     {
          public static void Main(string[] args)
          {
               using var host = WebHost.CreateDefaultBuilder(args)
                   .ConfigureLogging(logging =>
                   {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);
                   })
                   .UseStartup<Startup>()
                   .UseUrls(EndpointsConstants.SubscribersAddress)
                   .Build();

               host.Start();

               Subscribe(host);

               Console.WriteLine("Press enter to exit");
               Console.ReadLine();
               host.StopAsync().GetAwaiter().GetResult();
          }

          private static void Subscribe(IWebHost host)
          {
               using var channel = GrpcChannel.ForAddress(
                   EndpointsConstants.BrokerAddress);

               var client = new Subscriber.SubscriberClient(channel);

               var address = host.ServerFeatures
                   .Get<IServerAddressesFeature>()
                   ?.Addresses.FirstOrDefault()
                   ?? throw new InvalidOperationException("Receiver has no listening address.");

               // Certificatul de dezvoltare HTTPS este emis doar pentru "localhost", nu pentru 127.0.0.1.
               address = address.Replace("127.0.0.1", "localhost");

               Console.WriteLine($"Subscriber listening at: {address}");

               Console.Write("Enter the news category (e.g. sport, tech): ");
               var category = Console.ReadLine()?.Trim().ToLowerInvariant();

               if (string.IsNullOrWhiteSpace(category))
               {
                    Console.WriteLine("Invalid category.");
                    return;
               }

               var request = new SubscribeRequest()
               {
                    Category = category,
                    Address = address
               };

               try
               {
                    var reply = client.Subscribe(request, deadline: DateTime.UtcNow.AddSeconds(5));
                    Console.WriteLine($"Subscribed reply: {reply.IsSuccess}");
               }
               catch (Exception e)
               {
                    Console.WriteLine($"Error subscribing: {e.Message}");
               }
          }
     }
}
