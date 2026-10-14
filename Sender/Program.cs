using System;
using Grpc.Net.Client;
using Common;
using GrpcAgent;

namespace Sender
{
     class Program
     {
          static async Task Main(string[] args)
          {
               Console.WriteLine("Publisher");

               using var channel = GrpcChannel.ForAddress(EndpointsConstants.BrokerAddress);
               var client = new Publisher.PublisherClient(channel);

               while(true)
               {
                    Console.Write("Enter the news category (e.g. sport, tech): ");
                    var input = Console.ReadLine();
                    if (input is null)
                    {
                         break;
                    }

                    var category = input.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(category))
                    {
                         Console.WriteLine("Invalid category.");
                         continue;
                    }

                    Console.Write("Enter title: ");
                    var title = Console.ReadLine();
                    Console.Write("Enter content: ");
                    var content = Console.ReadLine();
                    Console.Write("Persistent? (y/N): ");
                    var persistent = Console.ReadLine();
                    if (title is null || content is null || persistent is null)
                    {
                         break;
                    }

                    var request = new PublishRequest()
                    {
                         News = new News()
                         {
                              Category = category,
                              Title = title.Trim(),
                              Content = content,
                              IsPersistent = persistent.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                         }
                    };

                    try
                    {
                         var reply = await client.PublishMessageAsync(request,
                              deadline: DateTime.UtcNow.AddSeconds(5));
                         Console.WriteLine($"Publish Reply: {reply.IsSuccess} (id {reply.Id})");
                    }
                    catch (Exception e)
                    {
                         Console.WriteLine($"Error publishing the message: {e.Message}");
                    }
               }

          }
     }

}
