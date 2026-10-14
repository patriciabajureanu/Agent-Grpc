using System.Collections.Concurrent;
using Broker.Models;
using Broker.Services.Interfaces;

namespace Broker.Services
{
     public class MessageStorageService : IMessageStorageService
     {
          private readonly ConcurrentQueue<Message> _messages;

          public MessageStorageService()
          {
               _messages = new ConcurrentQueue<Message>();
          }
          public void Add(Message message)
          {
               _messages.Enqueue(message);
          }

          public Message? GetNext()
          {
               _messages.TryDequeue(out var message);

               return message;
          }

          public bool IsEmpty()
          {
               return _messages.IsEmpty;
          }
     }
}
