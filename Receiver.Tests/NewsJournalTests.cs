using GrpcAgent;
using Receiver.Services;

namespace Receiver.Tests
{
     public class NewsJournalTests : IDisposable
     {
          private readonly string _path = Path.Combine(Path.GetTempPath(), $"received-{Guid.NewGuid()}.jsonl");

          private static News CreateNews() => new()
          {
               MessageId = Guid.NewGuid().ToString(),
               Category = "sport",
               Title = "t",
               Content = "c",
               SchemaVersion = 1
          };

          [Fact]
          public async Task Same_news_produces_the_effect_only_once()
          {
               var journal = new NewsJournal(_path);
               var news = CreateNews();

               Assert.True(await journal.TryAppendAsync(news, CancellationToken.None));
               Assert.False(await journal.TryAppendAsync(news, CancellationToken.None));

               Assert.Single(File.ReadAllLines(_path));
          }

          [Fact]
          public async Task Redelivery_after_a_crash_before_ack_is_recognized_as_duplicate()
          {
               var news = CreateNews();
               // Efectul a fost aplicat, apoi procesul a cazut inainte sa trimita ACK.
               await new NewsJournal(_path).TryAppendAsync(news, CancellationToken.None);

               var restarted = new NewsJournal(_path);

               Assert.Equal(1, restarted.Count);
               Assert.False(await restarted.TryAppendAsync(news, CancellationToken.None));
               Assert.Single(File.ReadAllLines(_path));
          }

          [Fact]
          public async Task Partially_written_line_is_ignored_and_the_news_is_processed_again()
          {
               var news = CreateNews();
               await File.WriteAllTextAsync(_path, "{\"messageId\": \"" + news.MessageId);

               var journal = new NewsJournal(_path);

               Assert.Equal(0, journal.Count);
               Assert.True(await journal.TryAppendAsync(news, CancellationToken.None));
          }

          public void Dispose()
          {
               File.Delete(_path);
          }
     }
}
