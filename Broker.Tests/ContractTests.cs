using Google.Protobuf;
using GrpcAgent;

namespace Broker.Tests
{
     // Verifica compatibilitatea contractului agent.v1: mesajele produse de versiuni mai vechi sau mai noi raman citibile.
     public class ContractTests
     {
          [Fact]
          public void News_field_numbers_are_stable()
          {
               var fields = News.Descriptor.Fields.InFieldNumberOrder().ToDictionary(x => x.Name, x => x.FieldNumber);

               Assert.Equal(new Dictionary<string, int>
               {
                    ["message_id"] = 1,
                    ["category"] = 2,
                    ["title"] = 3,
                    ["content"] = 4,
                    ["occurred_at"] = 5,
                    ["is_persistent"] = 6,
                    ["correlation_id"] = 7,
                    ["message_type"] = 8,
                    ["schema_version"] = 9,
               }, fields);
               Assert.Equal("agent.v1", News.Descriptor.File.Package);
          }

          [Fact]
          public void Message_from_a_newer_producer_is_read_and_forwarded_with_its_unknown_fields()
          {
               var news = new News { MessageId = "m-1", Category = "sport", Title = "t", Content = "c", SchemaVersion = 1 };
               // Simuleaza un camp nou (numarul 50) adaugat de un producator v1.1 si necunoscut acestui consumator.
               var bytes = news.ToByteArray().Concat(new byte[] { 0x92, 0x03, 0x02, (byte)'h', (byte)'i' }).ToArray();

               var parsed = News.Parser.ParseFrom(bytes);

               Assert.Equal("sport", parsed.Category);
               Assert.Equal(bytes, parsed.Clone().ToByteArray());
          }

          [Fact]
          public void Message_from_an_older_producer_without_envelope_fields_is_read_with_defaults()
          {
               // Doar campurile 2..4, asa cum le trimitea varianta initiala a contractului.
               var old = new News { Category = "tech", Title = "t", Content = "c" }.ToByteArray();

               var parsed = News.Parser.ParseFrom(old);

               Assert.Equal("tech", parsed.Category);
               Assert.Equal("", parsed.MessageId);
               Assert.Equal(0, parsed.SchemaVersion);
          }
     }
}
