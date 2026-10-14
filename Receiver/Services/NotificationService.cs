using Common;
using Grpc.Core;
using GrpcAgent;
using Microsoft.Extensions.Logging;

namespace Receiver.Services
{
     public class NotificationService : Notifier.NotifierBase
     {
          private readonly NewsJournal _journal;
          private readonly ILogger<NotificationService> _logger;

          public NotificationService(NewsJournal journal, ILogger<NotificationService> logger)
          {
               _journal = journal;
               _logger = logger;
          }

          public override async Task<NotifyReply> Notify(NotifyRequest request, ServerCallContext context)
          {
               var news = request.News;
               using var scope = _logger.BeginScope(LogEvents.Scope(
                    news?.CorrelationId ?? context.RequestHeaders.GetValue(ContractConstants.CorrelationIdHeader) ?? "",
                    news?.MessageId ?? ""));

               if (news is null || string.IsNullOrWhiteSpace(news.MessageId))
               {
                    return Reject("news with a message_id is required");
               }

               if (news.SchemaVersion != ContractConstants.SchemaVersion)
               {
                    return Reject($"schema_version {news.SchemaVersion} is not supported");
               }

               try
               {
                    if (!await _journal.TryAppendAsync(news, context.CancellationToken))
                    {
                         // Duplicat: efectul exista deja, deci confirmam fara sa il repetam.
                         _logger.LogInformation(LogEvents.ReceivedDuplicate, "Duplicate news acknowledged without a new effect, {Result}", AckStatus.Ack);
                         return Reply(AckStatus.Ack, "duplicate");
                    }
               }
               catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
               {
                    _logger.LogError(LogEvents.JournalUnavailable, exception, "Journal {Path} unavailable, {Result}", _journal.Path, AckStatus.NackRetry);
                    return Reply(AckStatus.NackRetry, $"journal unavailable: {exception.Message}");
               }

               _logger.LogInformation(LogEvents.NewsReceived, "News received in category {Category}, {Result}", news.Category, AckStatus.Ack);

               Console.WriteLine($"[{news.Category}] {news.Title} ({news.OccurredAt.ToDateTime():u}, {(news.IsPersistent ? "persistent" : "transient")})");
               Console.WriteLine($"    {news.Content}");
               Console.WriteLine($"    id={news.MessageId} correlation={news.CorrelationId}");
               return Reply(AckStatus.Ack, "");
          }

          private NotifyReply Reject(string reason)
          {
               _logger.LogWarning(LogEvents.ReceivedRejected, "News rejected, {Result}: {Reason}", AckStatus.NackReject, reason);
               return Reply(AckStatus.NackReject, reason);
          }

          private static NotifyReply Reply(AckStatus status, string reason) => new() { Status = status, Reason = reason };
     }
}
