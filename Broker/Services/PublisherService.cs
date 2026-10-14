using Broker.Services.Interfaces;
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
               var input = request.News;
               if (input is null
                    || string.IsNullOrWhiteSpace(input.Category)
                    || string.IsNullOrWhiteSpace(input.Title)
                    || string.IsNullOrWhiteSpace(input.Content))
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument,
                         "News with a category, a title and a content is required."));
               }

               // Id-ul si data publicarii sunt atribuite de Broker, nu de Sender.
               var news = new News()
               {
                    Id = Guid.NewGuid().ToString(),
                    Category = input.Category.Trim().ToLowerInvariant(),
                    Title = input.Title.Trim(),
                    Content = input.Content,
                    PublishedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                    IsPersistent = input.IsPersistent
               };
               Console.WriteLine($"Received: [{news.Category}] {news.Title} persistent={news.IsPersistent} id={news.Id}");

               _messageStorage.Add(news);

               return Task.FromResult(new PublishReply()
               {
                    IsSuccess = true,
                    Id = news.Id
               });
          }
     }
}
