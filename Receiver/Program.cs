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
                   // RECEIVER_URLS=https://0.0.0.0:5002 il face accesibil din retea (pentru Broker pe alt calculator).
                   .UseUrls(Environment.GetEnvironmentVariable("RECEIVER_URLS") ?? EndpointsConstants.SubscribersAddress)
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

               // Adresa la care Broker-ul il apeleaza: RECEIVER_PUBLIC_ADDRESS cand Broker-ul e pe alt calculator,
               // altfel "localhost", pentru ca certificatul de dezvoltare este emis doar pentru localhost.
               address = Environment.GetEnvironmentVariable("RECEIVER_PUBLIC_ADDRESS") ?? address.Replace("127.0.0.1", "localhost");

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
