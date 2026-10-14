using Broker.Services.Interfaces;
using Grpc.Core;
using GrpcAgent;

namespace Broker.Services
{
     public class SenderWorker : BackgroundService
     {
          private readonly IMessageStorageService _messageStorage;
          private readonly IConnectionStorageService _connectionStorage;

          public SenderWorker(IMessageStorageService messageStorage,
               IConnectionStorageService connectionStorage)
          {
               _messageStorage = messageStorage;
               _connectionStorage = connectionStorage;
          }

          protected override async Task ExecuteAsync(CancellationToken stoppingToken)
          {
               using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

               do
               {
                    while (!stoppingToken.IsCancellationRequested
                         && _messageStorage.GetNext() is { } news)
                    {
                         var connections = _connectionStorage.GetConnectionsByCategory(news.Category);

                         foreach (var connection in connections)
                         {
                              var client = new Notifier.NotifierClient(connection.Channel);
                              var request = new NotifyRequest() { News = news };

                              try
                              {
                                   var reply = await client.NotifyAsync(request,
                                        deadline: DateTime.UtcNow.AddSeconds(5),
                                        cancellationToken: stoppingToken);
                                   Console.WriteLine($"Notified subscriber {connection.Address} with {news.Id}. Response: {reply.IsSuccess}");
                              }
                              catch (RpcException) when (stoppingToken.IsCancellationRequested)
                              {
                                   return;
                              }
                              catch (RpcException exception)
                              {
                                   if (exception.StatusCode == StatusCode.Unavailable)
                                   {
                                        _connectionStorage.Remove(connection.Address);
                                   }
                                   Console.WriteLine($"RPC Error notifying subscriber {connection.Address}. {exception.Message}");
                              }
                              catch (Exception exception)
                              {
                                   Console.WriteLine($"Error notifying subscriber {connection.Address}. {exception.Message}");
                              }
                         }
                    }
               } while (await timer.WaitForNextTickAsync(stoppingToken));
          }
     }
}
