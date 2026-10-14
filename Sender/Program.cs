using System;
using Grpc.Net.Client;
using Common;
using Grpc.Core;
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

                    var correlationId = Guid.NewGuid().ToString();
                    var request = new PublishRequest()
                    {
                         News = new News()
                         {
                              MessageId = Guid.NewGuid().ToString(),
                              CorrelationId = correlationId,
                              MessageType = ContractConstants.NewsPublished,
                              SchemaVersion = ContractConstants.SchemaVersion,
                              Category = category,
                              Title = title.Trim(),
                              Content = content,
                              IsPersistent = persistent.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                         }
                    };

                    try
                    {
                         var reply = await client.PublishMessageAsync(request,
                              new Metadata { { ContractConstants.CorrelationIdHeader, correlationId } },
                              deadline: DateTime.UtcNow.AddSeconds(5));
                         Console.WriteLine($"Published news {reply.MessageId} (correlation {correlationId})");
                    }
                    catch (Exception e)
                    {
                         Console.WriteLine($"Error publishing the message: {e.Message}");
                    }
               }

          }
     }

}
