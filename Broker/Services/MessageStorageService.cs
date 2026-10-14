using System.Collections.Concurrent;
using GrpcAgent;
using Broker.Services.Interfaces;

namespace Broker.Services
{
     public class MessageStorageService : IMessageStorageService
     {
          private readonly ConcurrentQueue<News> _messages;

          public MessageStorageService()
          {
               _messages = new ConcurrentQueue<News>();
          }
          public void Add(News news)
          {
               _messages.Enqueue(news);
          }

          public News? GetNext()
          {
               _messages.TryDequeue(out var news);

               return news;
          }

          public bool IsEmpty()
          {
               return _messages.IsEmpty;
          }
     }
}
