using Broker.Models;
using Broker.Services.Interfaces;
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
               if (string.IsNullOrWhiteSpace(request.Topic))
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Topic is required."));
               }

               var topic = request.Topic.Trim().ToLowerInvariant();
               Console.WriteLine($"Received: {topic} {request.Content}");

               var message = new Message(topic, request.Content);
               _messageStorage.Add(message);

               return Task.FromResult(new PublishReply()
               {
                    IsSuccess = true
               });
          }
     }
}
