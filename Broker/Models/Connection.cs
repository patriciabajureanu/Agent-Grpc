using Grpc.Net.Client;

namespace Broker.Models
{
     public class Connection
     {
          public Connection(string address, string category)
          {
               Address = address;
               Category = category;
               Channel = GrpcChannel.ForAddress(address);
          }
          public string Address { get; }
          public string Category { get; }

          public GrpcChannel Channel { get; }
     }
}
