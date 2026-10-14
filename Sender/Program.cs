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

               AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
               using var channel = GrpcChannel.ForAddress(EndpointsConstants.BrokerAddress);
               var client = new Publisher.PublisherClient(channel);

               while(true)
               {
                    Console.Write("Enter the topic: ");
                    var input = Console.ReadLine();
                    if (input is null)
                    {
                         break;
                    }

                    var topic = input.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(topic))
                    {
                         Console.WriteLine("Invalid topic.");
                         continue;
                    }

                    Console.Write("Enter content: ");
                    var content = Console.ReadLine();
                    if (content is null)
                    {
                         break;
                    }

                    var request = new PublishRequest() { Topic = topic, Content = content };

                    try
                    {
                         var reply = await client.PublishMessageAsync(request,
                              deadline: DateTime.UtcNow.AddSeconds(5));
                         Console.WriteLine($"Publish Reply: {reply.IsSuccess}");
                    }
                    catch (Exception e)
                    {
                         Console.WriteLine($"Error publishing the message: {e.Message}");
                    }
               }

          }
     }

}
