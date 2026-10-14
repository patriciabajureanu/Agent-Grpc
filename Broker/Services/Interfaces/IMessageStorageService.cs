using GrpcAgent;

namespace Broker.Services.Interfaces
{
     public interface IMessageStorageService
     {
          // Adauga stirea in coada de livrare; o stire deja cunoscuta (acelasi id) este doar repusa in coada.
          void Add(News news);

          News? GetNext();

          // Stirea a fost livrata: nu mai este pastrata si nu mai intra in backup.
          void MarkConsumed(string id);

          // Stirile persistente care nu au fost inca livrate (inclusiv cele aflate in curs de livrare).
          IList<News> GetUnconsumedPersistent();

     }
}
