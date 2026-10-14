using Broker.Models;
using Broker.Services.Interfaces;
using Grpc.Core;
using GrpcAgent;


namespace Broker.Services
{
     public class SubscriberService : Subscriber.SubscriberBase
     {
          private readonly IConnectionStorageService _connectionStorage;
          public SubscriberService(IConnectionStorageService connectionStorage) 
          {
               _connectionStorage = connectionStorage;
          
          }
          public override Task<SubscribeReply> Subscribe(SubscribeRequest request, ServerCallContext context)
          {
               if (string.IsNullOrWhiteSpace(request.Topic)
                    || !Uri.TryCreate(request.Address, UriKind.Absolute, out var address)
                    || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                    || string.IsNullOrEmpty(address.Host))
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument,
                         "A topic and a valid HTTP(S) receiver address are required."));
               }

               Console.WriteLine($"New client trying to subscribe: {request.Address} {request.Topic}");

               try
               {
                    var connection = new Connection(request.Address, request.Topic.Trim().ToLowerInvariant());
                    _connectionStorage.Add(connection);
               }
               catch (Exception e)
               {
                    Console.WriteLine($"Could not add the new connection {request.Address} {request.Topic} {e.Message}");
                    return Task.FromResult(new SubscribeReply() { IsSuccess = false });
               }

               return Task.FromResult(new SubscribeReply()
               {
                    IsSuccess = true
               });

          }

     }
}
