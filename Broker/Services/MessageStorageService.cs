using System.Collections.Concurrent;
using Broker.Services.Interfaces;
using GrpcAgent;

namespace Broker.Services
{
     public class MessageStorageService : IMessageStorageService
     {
          // Coada da ordinea de livrare; dictionarul tine tot ce nu a fost livrat inca, inclusiv stirile scoase din coada.
          private readonly ConcurrentQueue<News> _queue;
          private readonly ConcurrentDictionary<string, News> _unconsumed;

          public MessageStorageService()
          {
               _queue = new ConcurrentQueue<News>();
               _unconsumed = new ConcurrentDictionary<string, News>();
          }

          public void Add(News news)
          {
               _unconsumed[news.Id] = news;
               _queue.Enqueue(news);
          }

          public News? GetNext()
          {
               _queue.TryDequeue(out var news);

               return news;
          }

          public void MarkConsumed(string id)
          {
               _unconsumed.TryRemove(id, out _);
          }
     }
}
