using Grpc.Core;
using GrpcAgent;

namespace Receiver.Services
{
     public class NotificationService : Notifier.NotifierBase
     {
          public override Task<NotifyReply> Notify(NotifyRequest request, ServerCallContext context)
          {
               var news = request.News;
               if (news is null)
               {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "News is required."));
               }

               Console.WriteLine($"[{news.Category}] {news.Title} ({news.OccurredAt.ToDateTime():u}, {(news.IsPersistent ? "persistent" : "transient")})");
               Console.WriteLine($"    {news.Content}");
               Console.WriteLine($"    id={news.MessageId} correlation={news.CorrelationId}");
               return Task.FromResult(new NotifyReply() { IsSuccess = true });
          }
     }
}
