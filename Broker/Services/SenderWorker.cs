using Broker.Models;
using Broker.Services.Interfaces;
using Common;
using Grpc.Core;
using GrpcAgent;

namespace Broker.Services
{
     public class SenderWorker : BackgroundService
     {
          // shortcut: fereastra de deduplicare este doar in memorie; dupa repornirea Broker-ului deduplicarea ramane la Receiver.
          private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromHours(1);

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
                    // Stirile fara niciun abonat care le-a primit raman in storage si sunt reincercate la urmatorul tick.
                    var undelivered = new List<StoredNews>();

                    while (!stoppingToken.IsCancellationRequested
                         && _messageStorage.GetNext() is { } stored)
                    {
                         var news = stored.News;
                         var delivered = false;
                         var connections = _connectionStorage.GetConnectionsByCategory(news.Category);

                         foreach (var connection in connections)
                         {
                              var client = new Notifier.NotifierClient(connection.Channel);
                              var request = new NotifyRequest() { News = news };

                              try
                              {
                                   var reply = await client.NotifyAsync(request,
                                        new Metadata { { ContractConstants.CorrelationIdHeader, news.CorrelationId } },
                                        deadline: DateTime.UtcNow.AddSeconds(5),
                                        cancellationToken: stoppingToken);
                                   delivered |= reply.IsSuccess;
                                   Console.WriteLine($"Notified subscriber {connection.Address} with {news.MessageId}. Response: {reply.IsSuccess}");
                              }
                              catch (RpcException) when (stoppingToken.IsCancellationRequested)
                              {
                                   _messageStorage.Requeue(stored);
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

                         if (delivered)
                         {
                              _messageStorage.MarkDelivered(stored);
                         }
                         else
                         {
                              undelivered.Add(stored);
                         }
                    }

                    undelivered.ForEach(_messageStorage.Requeue);
                    _messageStorage.PurgeCompleted(DateTime.UtcNow - DeduplicationWindow);
               } while (await timer.WaitForNextTickAsync(stoppingToken));
          }
     }
}
