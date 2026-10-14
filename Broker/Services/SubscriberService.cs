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
               var connection = new Connection(request.Address, request.Topic);
               _connectionStorage.Add(connection);

               Console.WriteLine($"New client subscribed: {request.Address} {request.Topic}");

               return Task.FromResult(new SubscribeReply()
               {
                    IsSuccess = true
               });

          }

     }
}
