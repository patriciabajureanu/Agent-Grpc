namespace Broker.Models
{
     public class Connection
     {
          public Connection(string address, string topic)
          {
               Address = address;
               Topic = topic;
          }
          public string Address { get; }
          public string Topic { get; }
     }
}
