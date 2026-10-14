using Microsoft.Extensions.Logging;

namespace Common
{
     // Numele evenimentelor din log-uri; apar in campul EventId.Name al fiecarei linii JSON.
     public static class LogEvents
     {
          public static readonly EventId NewsAccepted = new(1001, "news.accepted");
          public static readonly EventId NewsDuplicate = new(1002, "news.duplicate");
          public static readonly EventId NewsRejected = new(1003, "news.rejected");

          public static readonly EventId SubscriberAdded = new(1101, "subscriber.added");
          public static readonly EventId SubscriberRemoved = new(1102, "subscriber.removed");

          public static readonly EventId NewsDelivered = new(1201, "news.delivered");
          public static readonly EventId DeliveryFailed = new(1202, "news.delivery_failed");
          public static readonly EventId NewsDeadLettered = new(1203, "news.dead_lettered");

          public static readonly EventId BackupFailed = new(1302, "backup.failed");
          public static readonly EventId BackupRestored = new(1303, "backup.restored");
          public static readonly EventId BackupCorrupt = new(1304, "backup.corrupt");

          public static readonly EventId NewsReceived = new(1401, "news.received");
          public static readonly EventId ReceivedDuplicate = new(1402, "news.received_duplicate");
          public static readonly EventId ReceivedRejected = new(1403, "news.received_rejected");
          public static readonly EventId JournalUnavailable = new(1404, "journal.unavailable");

          public static readonly EventId PublishSucceeded = new(1501, "publish.succeeded");
          public static readonly EventId PublishRetry = new(1502, "publish.retry");
          public static readonly EventId PublishFailed = new(1503, "publish.failed");

          // Campurile comune adaugate fiecarei linii de log dintr-un flux.
          public static Dictionary<string, object> Scope(string correlationId, string messageId) => new()
          {
               ["CorrelationId"] = correlationId,
               ["MessageId"] = messageId
          };
     }
}
