using Broker.Services;
using GrpcAgent;

namespace Broker.Tests
{
     public class MessageStorageServiceTests
     {
          private static News CreateNews() => new() { MessageId = Guid.NewGuid().ToString(), Category = "sport", Title = "t", Content = "c" };

          [Fact]
          public void Same_message_id_is_stored_and_queued_only_once()
          {
               var storage = new MessageStorageService();
               var news = CreateNews();

               Assert.True(storage.TryAdd(news, out var first));
               Assert.False(storage.TryAdd(news.Clone(), out var second));

               Assert.Same(first, second);
               Assert.Same(first, storage.GetNext());
               Assert.Null(storage.GetNext());
          }

          [Fact]
          public void Delivered_news_keeps_its_status_until_the_deduplication_window_passes()
          {
               var storage = new MessageStorageService();
               storage.TryAdd(CreateNews(), out var stored);
               storage.MarkDelivered(stored);

               storage.PurgeCompleted(DateTime.UtcNow.AddMinutes(-1));
               Assert.Equal(NewsStatus.Delivered, storage.Find(stored.News.MessageId)?.Status);

               storage.PurgeCompleted(DateTime.UtcNow.AddMinutes(1));
               Assert.Null(storage.Find(stored.News.MessageId));
          }
     }
}
