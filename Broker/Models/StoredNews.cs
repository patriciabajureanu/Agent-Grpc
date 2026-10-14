using GrpcAgent;

namespace Broker.Models
{
     // Starea unei stiri in Broker; este modificata doar de SenderWorker si citita de GetNewsStatus.
     public class StoredNews
     {
          public StoredNews(News news)
          {
               News = news;
               Status = NewsStatus.Pending;
               UpdatedAt = DateTime.UtcNow;
          }

          public News News { get; }

          public NewsStatus Status { get; set; }

          public int Attempts { get; set; }

          public DateTime NextAttemptAt { get; set; }

          public string LastError { get; set; } = "";

          public DateTime UpdatedAt { get; set; }
     }
}
