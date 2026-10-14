using Broker.Models;
using Broker.Services.Interfaces;
using Common;
using Grpc.Core;
using GrpcAgent;


namespace Broker.Services
{
     public class SubscriberService : Subscriber.SubscriberBase
     {
          private readonly IConnectionStorageService _connectionStorage;
          private readonly ILogger<SubscriberService> _logger;

          public SubscriberService(IConnectionStorageService connectionStorage, ILogger<SubscriberService> logger)
          {
               _connectionStorage = connectionStorage;
               _logger = logger;
          }

          public override Task<SubscribeReply> Subscribe(SubscribeRequest request, ServerCallContext context)
          {
               if (string.IsNullOrWhiteSpace(request.Category)
                    || !Uri.TryCreate(request.Address, UriKind.Absolute, out var address)
                    || address.Scheme != Uri.UriSchemeHttps
                    || string.IsNullOrEmpty(address.Host))
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument,
                         "A category and a valid HTTPS receiver address are required."));
               }

               try
               {
                    var connection = new Connection(request.Address, request.Category.Trim().ToLowerInvariant());
                    _connectionStorage.Add(connection);
                    _logger.LogInformation(LogEvents.SubscriberAdded, "Subscriber {Subscriber} added to category {Category}",
                         connection.Address, connection.Category);
               }
               catch (Exception e)
               {
                    _logger.LogError(LogEvents.SubscriberAdded, e, "Could not add subscriber {Subscriber} to category {Category}",
                         request.Address, request.Category);
                    return Task.FromResult(new SubscribeReply() { IsSuccess = false });
               }

               return Task.FromResult(new SubscribeReply()
               {
                    IsSuccess = true
               });

          }

     }
}
