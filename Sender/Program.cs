using System;
using Grpc.Net.Client;
using Common;
using Grpc.Core;
using GrpcAgent;

namespace Sender
{
     class Program
     {
          private const int MaxAttempts = 4;
          private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
          private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

          static async Task Main(string[] args)
          {
               Console.WriteLine("Publisher");

               using var channel = GrpcChannel.ForAddress(EndpointsConstants.BrokerAddress);
               var client = new Publisher.PublisherClient(channel);

               while(true)
               {
                    Console.Write("Enter the news category (e.g. sport, tech) or 'status': ");
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

                    if (category == "status")
                    {
                         Console.Write("Enter the news id: ");
                         await PrintStatusAsync(client, Console.ReadLine()?.Trim() ?? "");
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
                              MessageId = Guid.NewGuid().ToString(),
                              CorrelationId = Guid.NewGuid().ToString(),
                              MessageType = ContractConstants.NewsPublished,
                              SchemaVersion = ContractConstants.SchemaVersion,
                              Category = category,
                              Title = title.Trim(),
                              Content = content,
                              IsPersistent = persistent.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                         }
                    };

                    await PublishAsync(client, request);
               }

          }

          // Reincearca doar erorile temporare, cu acelasi message_id: Broker-ul recunoaste repetarea si nu dubleaza stirea.
          private static async Task PublishAsync(Publisher.PublisherClient client, PublishRequest request)
          {
               var headers = new Metadata { { ContractConstants.CorrelationIdHeader, request.News.CorrelationId } };

               for (var attempt = 1; ; attempt++)
               {
                    try
                    {
                         var reply = await client.PublishMessageAsync(request, headers,
                              deadline: DateTime.UtcNow.AddSeconds(5));
                         Console.WriteLine($"Published news {reply.MessageId} (correlation {request.News.CorrelationId}){(reply.Duplicate ? ", already accepted earlier" : "")}");
                         return;
                    }
                    catch (RpcException e) when (RetryPolicy.IsTransient(e.StatusCode) && attempt < MaxAttempts)
                    {
                         var delay = RetryPolicy.GetDelay(attempt, BaseDelay, MaxDelay);
                         Console.WriteLine($"Attempt {attempt}/{MaxAttempts} failed with {e.StatusCode}; retrying in {delay.TotalMilliseconds:F0} ms");
                         await Task.Delay(delay);
                    }
                    catch (RpcException e)
                    {
                         Console.WriteLine($"Publishing news {request.News.MessageId} failed after {attempt} attempt(s): {e.StatusCode} {e.Status.Detail}");
                         return;
                    }
               }
          }

          private static async Task PrintStatusAsync(Publisher.PublisherClient client, string messageId)
          {
               try
               {
                    var reply = await client.GetNewsStatusAsync(new NewsStatusRequest() { MessageId = messageId },
                         deadline: DateTime.UtcNow.AddSeconds(5));
                    Console.WriteLine($"Status: {reply.Status}, attempts: {reply.Attempts} {reply.LastError}");
               }
               catch (RpcException e)
               {
                    Console.WriteLine($"Error reading the status: {e.StatusCode} {e.Status.Detail}");
               }
          }
     }

}
