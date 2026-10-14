using Broker.Models;
using GrpcAgent;

namespace Broker.Services.Interfaces
{
     public interface IMessageStorageService
     {
          // Adauga o stire noua in coada; intoarce false si inregistrarea existenta daca message_id este deja cunoscut.
          bool TryAdd(News news, out StoredNews stored);

          StoredNews? GetNext();

          // Repune in coada o stire inca nelivrata.
          void Requeue(StoredNews stored);

          void MarkDelivered(StoredNews stored);

          void MarkDeadLettered(StoredNews stored, string reason);

          StoredNews? Find(string messageId);

          // Stirile persistente inca nelivrate, inclusiv cele aflate in curs de livrare.
          IList<StoredNews> GetPendingPersistent();

          // Uita stirile finalizate mai vechi decat limita; dupa aceea message_id-ul lor nu mai este deduplicat.
          void PurgeCompleted(DateTime olderThan);

     }
}
