using Common;
using Grpc.Core;
using GrpcAgent;

namespace Receiver.Services
{
     public class NotificationService : Notifier.NotifierBase
     {
          private readonly NewsJournal _journal;

          public NotificationService(NewsJournal journal)
          {
               _journal = journal;
          }

          public override async Task<NotifyReply> Notify(NotifyRequest request, ServerCallContext context)
          {
               var news = request.News;
               if (news is null || string.IsNullOrWhiteSpace(news.MessageId))
               {
                    return Reply(AckStatus.NackReject, "news with a message_id is required");
               }

               if (news.SchemaVersion != ContractConstants.SchemaVersion)
               {
                    return Reply(AckStatus.NackReject, $"schema_version {news.SchemaVersion} is not supported");
               }

               try
               {
                    if (!await _journal.TryAppendAsync(news, context.CancellationToken))
                    {
                         // Duplicat: efectul exista deja, deci confirmam fara sa il repetam.
                         Console.WriteLine($"Duplicate news ignored: id={news.MessageId} correlation={news.CorrelationId}");
                         return Reply(AckStatus.Ack, "duplicate");
                    }
               }
               catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
               {
                    return Reply(AckStatus.NackRetry, $"journal unavailable: {exception.Message}");
               }

               Console.WriteLine($"[{news.Category}] {news.Title} ({news.OccurredAt.ToDateTime():u}, {(news.IsPersistent ? "persistent" : "transient")})");
               Console.WriteLine($"    {news.Content}");
               Console.WriteLine($"    id={news.MessageId} correlation={news.CorrelationId}");
               return Reply(AckStatus.Ack, "");
          }

          private static NotifyReply Reply(AckStatus status, string reason) => new() { Status = status, Reason = reason };
     }
}
