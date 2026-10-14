using Broker.Services;
using GrpcAgent;
using Microsoft.Extensions.Configuration;

namespace Broker.Tests
{
     public class PersistenceWorkerTests : IDisposable
     {
          private readonly string _backupPath = Path.Combine(Path.GetTempPath(), $"news-backup-{Guid.NewGuid()}.json");

          private PersistenceWorker CreateWorker(MessageStorageService storage)
          {
               var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Persistence:BackupPath"] = _backupPath })
                    .Build();
               return new PersistenceWorker(storage, configuration);
          }

          private static News CreateNews(string title, bool isPersistent) => new()
          {
               MessageId = Guid.NewGuid().ToString(),
               Category = "sport",
               Title = title,
               Content = "content",
               IsPersistent = isPersistent
          };

          [Fact]
          public async Task Restore_after_restart_returns_only_unconsumed_persistent_news()
          {
               var storage = new MessageStorageService();
               var kept = CreateNews("kept", isPersistent: true);
               var consumed = CreateNews("consumed", isPersistent: true);
               storage.TryAdd(kept, out _);
               storage.TryAdd(consumed, out var consumedEntry);
               storage.TryAdd(CreateNews("transient", isPersistent: false), out _);
               // O stire scoasa din coada dar inca nelivrata trebuie sa ajunga totusi in backup.
               storage.GetNext();
               storage.MarkDelivered(consumedEntry);

               await CreateWorker(storage).BackupAsync(CancellationToken.None);

               var restarted = new MessageStorageService();
               await CreateWorker(restarted).RestoreAsync(CancellationToken.None);

               var restored = Assert.Single(restarted.GetPendingPersistent());
               Assert.Equal(kept, restored.News);
               Assert.Equal(kept, restarted.GetNext()?.News);
               Assert.Null(restarted.GetNext());
          }

          [Fact]
          public async Task Corrupt_backup_is_moved_aside_and_broker_starts_empty()
          {
               await File.WriteAllTextAsync(_backupPath, "{ not json");
               var storage = new MessageStorageService();

               await CreateWorker(storage).RestoreAsync(CancellationToken.None);

               Assert.Empty(storage.GetPendingPersistent());
               Assert.False(File.Exists(_backupPath));
               Assert.True(File.Exists(_backupPath + ".corrupt"));
          }

          public void Dispose()
          {
               File.Delete(_backupPath);
               File.Delete(_backupPath + ".corrupt");
          }
     }
}
