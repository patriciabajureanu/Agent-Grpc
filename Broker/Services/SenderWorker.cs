using System.Diagnostics;
using Broker.Models;
using Broker.Services.Interfaces;
using Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
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
          private readonly int _maxAttempts;
          private readonly TimeSpan _baseDelay;
          private readonly TimeSpan _maxDelay;
          private readonly TimeSpan _pollInterval;
          private readonly string _deadLetterPath;
          private readonly ILogger<SenderWorker> _logger;

          public SenderWorker(IMessageStorageService messageStorage,
               IConnectionStorageService connectionStorage,
               IConfiguration configuration,
               ILogger<SenderWorker> logger)
          {
               _messageStorage = messageStorage;
               _connectionStorage = connectionStorage;
               _logger = logger;
               _maxAttempts = configuration.GetValue("Delivery:MaxAttempts", 5);
               _baseDelay = TimeSpan.FromMilliseconds(configuration.GetValue("Delivery:BaseDelayMs", 1000));
               _maxDelay = TimeSpan.FromMilliseconds(configuration.GetValue("Delivery:MaxDelayMs", 30000));
               _pollInterval = TimeSpan.FromMilliseconds(configuration.GetValue("Delivery:PollIntervalMs", 2000));
               _deadLetterPath = configuration["Delivery:DeadLetterPath"] ?? "dead-letter.jsonl";
          }

          protected override async Task ExecuteAsync(CancellationToken stoppingToken)
          {
               using var timer = new PeriodicTimer(_pollInterval);

               do
               {
                    // Stirile neconfirmate raman in storage si sunt reluate la un tick urmator.
                    var undelivered = new List<StoredNews>();

                    while (!stoppingToken.IsCancellationRequested
                         && _messageStorage.GetNext() is { } stored)
                    {
                         if (stored.NextAttemptAt > DateTime.UtcNow)
                         {
                              undelivered.Add(stored);
                              continue;
                         }

                         var connections = _connectionStorage.GetConnectionsByCategory(stored.News.Category);
                         if (connections.Count == 0)
                         {
                              // Asteptarea unui abonat nu este o incercare esuata si nu consuma din limita de reincercari.
                              undelivered.Add(stored);
                              continue;
                         }

                         using var scope = _logger.BeginScope(LogEvents.Scope(stored.News.CorrelationId, stored.News.MessageId));
                         var outcome = await DeliverAsync(stored.News, connections, stoppingToken);
                         if (outcome is null)
                         {
                              undelivered.Add(stored);
                              break;
                         }

                         var (status, error) = outcome.Value;
                         if (status == AckStatus.Ack)
                         {
                              _messageStorage.MarkDelivered(stored);
                         }
                         else if (status == AckStatus.NackReject)
                         {
                              await DeadLetterAsync(stored, $"rejected by subscriber: {error}");
                         }
                         else if (++stored.Attempts >= _maxAttempts)
                         {
                              await DeadLetterAsync(stored, $"{stored.Attempts} failed attempts, last error: {error}");
                         }
                         else
                         {
                              stored.LastError = error;
                              var delay = RetryPolicy.GetDelay(stored.Attempts, _baseDelay, _maxDelay);
                              stored.NextAttemptAt = DateTime.UtcNow + delay;
                              undelivered.Add(stored);
                              _logger.LogWarning(LogEvents.DeliveryFailed, "Delivery attempt {Attempt}/{MaxAttempts} failed: {Error}; next attempt in {DelayMs} ms",
                                   stored.Attempts, _maxAttempts, error, (int)delay.TotalMilliseconds);
                         }
                    }

                    undelivered.ForEach(_messageStorage.Requeue);
                    _messageStorage.PurgeCompleted(DateTime.UtcNow - DeduplicationWindow);
               } while (await timer.WaitForNextTickAsync(stoppingToken));
          }

          // Trimite stirea tuturor abonatilor categoriei. Rezultatul este ACK daca cel putin un abonat a confirmat,
          // NACK_REJECT daca a fost respinsa, altfel NACK_RETRY; null daca Broker-ul se opreste.
          private async Task<(AckStatus Status, string Error)?> DeliverAsync(News news, IList<Connection> connections, CancellationToken stoppingToken)
          {
               var result = AckStatus.NackRetry;
               var error = "";

               foreach (var connection in connections)
               {
                    var client = new Notifier.NotifierClient(connection.Channel);
                    var request = new NotifyRequest() { News = news };

                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                         var reply = await client.NotifyAsync(request,
                              new Metadata { { ContractConstants.CorrelationIdHeader, news.CorrelationId } },
                              deadline: DateTime.UtcNow.AddSeconds(5),
                              cancellationToken: stoppingToken);
                         _logger.LogInformation(LogEvents.NewsDelivered, "Subscriber {Subscriber} answered {Result} {Reason} in {DurationMs} ms",
                              connection.Address, reply.Status, reply.Reason, stopwatch.ElapsedMilliseconds);

                         if (reply.Status == AckStatus.Ack)
                         {
                              result = AckStatus.Ack;
                         }
                         else
                         {
                              error = $"{reply.Status} {reply.Reason}";
                              if (reply.Status == AckStatus.NackReject && result != AckStatus.Ack)
                              {
                                   result = AckStatus.NackReject;
                              }
                         }
                    }
                    catch (RpcException) when (stoppingToken.IsCancellationRequested)
                    {
                         return null;
                    }
                    catch (RpcException exception)
                    {
                         error = $"{exception.StatusCode} {exception.Status.Detail}";
                         _logger.LogWarning(LogEvents.DeliveryFailed, "Subscriber {Subscriber} failed with {Result} {Error} in {DurationMs} ms",
                              connection.Address, exception.StatusCode, exception.Status.Detail, stopwatch.ElapsedMilliseconds);
                         if (exception.StatusCode == StatusCode.Unavailable)
                         {
                              _connectionStorage.Remove(connection.Address);
                              _logger.LogWarning(LogEvents.SubscriberRemoved, "Subscriber {Subscriber} removed: unavailable", connection.Address);
                         }
                    }
                    catch (Exception exception)
                    {
                         error = exception.Message;
                         _logger.LogError(LogEvents.DeliveryFailed, exception, "Subscriber {Subscriber} failed in {DurationMs} ms",
                              connection.Address, stopwatch.ElapsedMilliseconds);
                    }
               }

               return (result, error);
          }

          // Stirile neprocesabile ies din livrare si sunt pastrate intr-un fisier separat pentru analiza manuala.
          private async Task DeadLetterAsync(StoredNews stored, string reason)
          {
               _messageStorage.MarkDeadLettered(stored, reason);
               var entry = new DeadLetterEntry()
               {
                    News = stored.News,
                    Reason = reason,
                    Attempts = stored.Attempts,
                    DeadLetteredAt = Timestamp.FromDateTime(DateTime.UtcNow)
               };

               try
               {
                    await File.AppendAllTextAsync(_deadLetterPath, JsonFormatter.Default.Format(entry) + Environment.NewLine);
               }
               catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
               {
                    _logger.LogError(LogEvents.NewsDeadLettered, exception, "Could not write the dead-letter file {Path}", _deadLetterPath);
               }

               _logger.LogError(LogEvents.NewsDeadLettered, "News moved to dead-letter after {Attempts} attempt(s): {Reason}", stored.Attempts, reason);
          }
     }
}
