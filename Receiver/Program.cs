using Common;
using Grpc.Net.Client;
using GrpcAgent;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Receiver.Services;

namespace Receiver
{
     public class Program
     {
          public static void Main(string[] args)
          {
               Console.Write("Enter the news category (e.g. sport, tech): ");
               var category = Console.ReadLine()?.Trim().ToLowerInvariant();

               if (string.IsNullOrWhiteSpace(category))
               {
                    Console.WriteLine("Invalid category.");
                    return;
               }

               // Jurnalul ramane pe disc intre reporniri, ca o stire deja procesata sa nu fie procesata a doua oara.
               var journal = new NewsJournal(Environment.GetEnvironmentVariable("RECEIVER_JOURNAL")
                    ?? $"received-{category}.jsonl");
               Console.WriteLine($"Journal: {journal.Path} ({journal.Count} news already processed)");

               using var host = WebHost.CreateDefaultBuilder(args)
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
                   .ConfigureServices(services => services.AddSingleton(journal))
                   .UseStartup<Startup>()
                   .UseUrls(EndpointsConstants.SubscribersAddress)
                   .Build();

               host.Start();

               Subscribe(host, category);

               Console.WriteLine("Press enter to exit");
               Console.ReadLine();
               host.StopAsync().GetAwaiter().GetResult();
          }

          private static void Subscribe(IWebHost host, string category)
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
