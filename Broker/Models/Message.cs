namespace Broker.Models
{
     public class Message
     {
          public Message(string topic, string content)
          {
               Topic = topic;
               Content = content;
          }


          public string Topic { get; set; }
          public string Content { get; set; }
     }
}
