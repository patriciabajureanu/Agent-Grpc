using Grpc.Net.Client;

namespace Broker.Models
{
     public class Connection
     {
          public Connection(string address, string category)
               : this(address, category, GrpcChannel.ForAddress(address))
          {
          }

          // Permite testelor sa conecteze Broker-ul la un Receiver pornit in proces.
          public Connection(string address, string category, GrpcChannel channel)
          {
               Address = address;
               Category = category;
               Channel = channel;
          }

          public string Address { get; }
          public string Category { get; }

          public GrpcChannel Channel { get; }
     }
}
