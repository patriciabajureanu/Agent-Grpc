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
          private readonly ILogger<PublisherService> _logger;

          public PublisherService(IMessageStorageService messageStorageService, ILogger<PublisherService> logger)
          {
               _messageStorage = messageStorageService;
               _logger = logger;
          }

          public override Task<PublishReply> PublishMessage(PublishRequest request, ServerCallContext context)
          {
               var correlationId = !string.IsNullOrWhiteSpace(request.News?.CorrelationId)
                    ? request.News.CorrelationId
                    : context.RequestHeaders.GetValue(ContractConstants.CorrelationIdHeader) ?? Guid.NewGuid().ToString();
               using var scope = _logger.BeginScope(LogEvents.Scope(correlationId, request.News?.MessageId ?? ""));

               News news;
               try
               {
                    news = Validate(request.News, correlationId);
               }
               catch (RpcException exception)
               {
                    _logger.LogWarning(LogEvents.NewsRejected, "Publish rejected: {Result} {Error}", exception.StatusCode, exception.Status.Detail);
                    throw;
               }

               if (!_messageStorage.TryAdd(news, out var existing))
               {
                    // Repetarea unei publicari (de exemplu dupa un raspuns pierdut) nu creeaza o a doua livrare.
                    if (existing.News.Category != news.Category
                         || existing.News.Title != news.Title
                         || existing.News.Content != news.Content)
                    {
                         _logger.LogWarning(LogEvents.NewsRejected, "Publish rejected: {Result} {Error}", StatusCode.AlreadyExists, "message_id reused for different news");
                         throw new RpcException(new Status(StatusCode.AlreadyExists,
                              $"message_id {news.MessageId} was already used for different news."));
                    }

                    _logger.LogInformation(LogEvents.NewsDuplicate, "Duplicate publish acknowledged, status {Result}", existing.Status);
                    return Task.FromResult(new PublishReply() { MessageId = news.MessageId, Duplicate = true });
               }

               _logger.LogInformation(LogEvents.NewsAccepted, "News accepted in category {Category}, persistent {IsPersistent}",
                    news.Category, news.IsPersistent);

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
          private static News Validate(News? input, string correlationId)
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
