using Google.Protobuf;
using GrpcAgent;

namespace Receiver.Services
{
     // Efectul local al Receiver-ului: fiecare stire procesata este o linie JSON in jurnal.
     // Efectul si marcarea "procesat" sunt aceeasi scriere, deci o cadere dupa efect si inainte de ACK
     // duce la o relivrare recunoscuta ca duplicat, nu la un al doilea efect.
     public class NewsJournal
     {
          private readonly HashSet<string> _processedIds;
          private readonly SemaphoreSlim _lock = new(1, 1);

          public NewsJournal(string path)
          {
               Path = path;
               _processedIds = new HashSet<string>();

               if (!File.Exists(path))
               {
                    return;
               }

               foreach (var line in File.ReadLines(path).Where(x => x.Length > 0))
               {
                    try
                    {
                         _processedIds.Add(News.Parser.ParseJson(line).MessageId);
                    }
                    catch (Exception exception) when (exception is InvalidProtocolBufferException or InvalidJsonException)
                    {
                         // O linie scrisa partial la o cadere inseamna ca efectul nu s-a terminat; stirea va fi relivrata.
                    }
               }
          }

          public string Path { get; }

          public int Count => _processedIds.Count;

          // Intoarce false daca stirea fusese deja procesata.
          public async Task<bool> TryAppendAsync(News news, CancellationToken cancellationToken)
          {
               await _lock.WaitAsync(cancellationToken);
               try
               {
                    if (_processedIds.Contains(news.MessageId))
                    {
                         return false;
                    }

                    await File.AppendAllTextAsync(Path, JsonFormatter.Default.Format(news) + Environment.NewLine, cancellationToken);
                    _processedIds.Add(news.MessageId);
                    return true;
               }
               finally
               {
                    _lock.Release();
               }
          }
     }
}
