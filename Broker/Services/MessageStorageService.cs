using System.Collections.Concurrent;
using Broker.Models;
using Broker.Services.Interfaces;
using GrpcAgent;

namespace Broker.Services
{
     public class MessageStorageService : IMessageStorageService
     {
          // Coada da ordinea de livrare; dictionarul tine toate stirile cunoscute dupa message_id (deduplicare si stare).
          private readonly ConcurrentQueue<StoredNews> _queue;
          private readonly ConcurrentDictionary<string, StoredNews> _known;

          public MessageStorageService()
          {
               _queue = new ConcurrentQueue<StoredNews>();
               _known = new ConcurrentDictionary<string, StoredNews>();
          }

          public bool TryAdd(News news, out StoredNews stored)
          {
               var candidate = new StoredNews(news);
               stored = _known.GetOrAdd(news.MessageId, candidate);
               if (!ReferenceEquals(stored, candidate))
               {
                    return false;
               }

               _queue.Enqueue(stored);
               return true;
          }

          public StoredNews? GetNext()
          {
               _queue.TryDequeue(out var stored);

               return stored;
          }

          public void Requeue(StoredNews stored)
          {
               _queue.Enqueue(stored);
          }

          public void MarkDelivered(StoredNews stored)
          {
               stored.Status = NewsStatus.Delivered;
               stored.UpdatedAt = DateTime.UtcNow;
          }

          public StoredNews? Find(string messageId)
          {
               return _known.TryGetValue(messageId, out var stored) ? stored : null;
          }

          public IList<StoredNews> GetPendingPersistent()
          {
               return _known.Values.Where(x => x.Status == NewsStatus.Pending && x.News.IsPersistent).ToList();
          }

          public void PurgeCompleted(DateTime olderThan)
          {
               foreach (var (id, stored) in _known)
               {
                    if (stored.Status != NewsStatus.Pending && stored.UpdatedAt < olderThan)
                    {
                         _known.TryRemove(id, out _);
                    }
               }
          }
     }
}
