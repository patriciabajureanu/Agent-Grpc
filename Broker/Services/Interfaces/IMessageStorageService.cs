using GrpcAgent;

namespace Broker.Services.Interfaces
{
     public interface IMessageStorageService
     {
          void Add(News news);

          News? GetNext();

          bool IsEmpty();

     }
}
