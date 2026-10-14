using Broker.Services.Interfaces;
using Common;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcAgent;

namespace Broker.Services
{
     public class PublisherService : Publisher.PublisherBase
     {
          private readonly IMessageStorageService _messageStorage;
          public PublisherService(IMessageStorageService messageStorageService) 
          {
               _messageStorage = messageStorageService;
          }
          public override Task<PublishReply> PublishMessage(PublishRequest request, ServerCallContext context)
          {
               var news = Validate(request.News, context);

               if (!_messageStorage.TryAdd(news, out var existing))
               {
                    // Repetarea unei publicari (de exemplu dupa un raspuns pierdut) nu creeaza o a doua livrare.
                    if (existing.News.Category != news.Category
                         || existing.News.Title != news.Title
                         || existing.News.Content != news.Content)
                    {
                         throw new RpcException(new Status(StatusCode.AlreadyExists,
                              $"message_id {news.MessageId} was already used for different news."));
                    }

                    Console.WriteLine($"Duplicate publish ignored: id={news.MessageId} correlation={news.CorrelationId}");
                    return Task.FromResult(new PublishReply() { MessageId = news.MessageId, Duplicate = true });
               }

               Console.WriteLine($"Received: [{news.Category}] {news.Title} persistent={news.IsPersistent} id={news.MessageId} correlation={news.CorrelationId}");

               return Task.FromResult(new PublishReply()
               {
                    MessageId = news.MessageId
               });
          }

          public override Task<NewsStatusReply> GetNewsStatus(NewsStatusRequest request, ServerCallContext context)
          {
               var stored = _messageStorage.Find(request.MessageId);
               if (stored is null)
               {
                    return Task.FromResult(new NewsStatusReply() { Status = NewsStatus.Unknown });
               }

               return Task.FromResult(new NewsStatusReply()
               {
                    Status = stored.Status,
                    Attempts = stored.Attempts,
                    LastError = stored.LastError
               });
          }

          // Valideaza plicul primit si intoarce copia normalizata pe care o pastreaza Broker-ul.
          private static News Validate(News? input, ServerCallContext context)
          {
               if (input is null
                    || !Guid.TryParse(input.MessageId, out _)
                    || string.IsNullOrWhiteSpace(input.Category)
                    || string.IsNullOrWhiteSpace(input.Title)
                    || string.IsNullOrWhiteSpace(input.Content))
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument,
                         "News with a GUID message_id, a category, a title and a content is required."));
               }

               if (input.SchemaVersion != ContractConstants.SchemaVersion)
               {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                         $"schema_version {input.SchemaVersion} is not supported; expected {ContractConstants.SchemaVersion}."));
               }

               if (input.MessageType != ContractConstants.NewsPublished)
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument,
                         $"message_type must be '{ContractConstants.NewsPublished}'."));
               }

               var correlationId = !string.IsNullOrWhiteSpace(input.CorrelationId)
                    ? input.CorrelationId
                    : context.RequestHeaders.GetValue(ContractConstants.CorrelationIdHeader) ?? Guid.NewGuid().ToString();

               // Clone pastreaza si campurile necunoscute trimise de un producator mai nou.
               var news = input.Clone();
               news.Category = input.Category.Trim().ToLowerInvariant();
               news.Title = input.Title.Trim();
               news.CorrelationId = correlationId;
               news.OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow);
               return news;
          }
     }
}
