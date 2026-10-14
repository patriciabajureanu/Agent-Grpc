using Broker.Models;
using Broker.Services.Interfaces;
using Common;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcAgent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Receiver.Services;

namespace Broker.Tests
{
     // Broker si Receiver reali, pornite in proces; comunicarea trece prin gRPC, fara retea.
     public class IntegrationTests : IDisposable
     {
          private readonly string _directory = Path.Combine(Path.GetTempPath(), $"agent-grpc-{Guid.NewGuid()}");
          private readonly List<IDisposable> _disposables = new();

          public IntegrationTests()
          {
               Directory.CreateDirectory(_directory);
          }

          private string BackupPath => Path.Combine(_directory, "news-backup.json");
          private string DeadLetterPath => Path.Combine(_directory, "dead-letter.jsonl");
          private string JournalPath => Path.Combine(_directory, "received.jsonl");

          private TestServer StartBroker()
          {
               var settings = new Dictionary<string, string?>
               {
                    ["Persistence:BackupPath"] = BackupPath,
                    ["Persistence:IntervalSeconds"] = "1",
                    ["Delivery:MaxAttempts"] = "3",
                    ["Delivery:BaseDelayMs"] = "10",
                    ["Delivery:MaxDelayMs"] = "50",
                    ["Delivery:PollIntervalMs"] = "50",
                    ["Delivery:DeadLetterPath"] = DeadLetterPath,
               };
               var server = new TestServer(new WebHostBuilder()
                    .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                    .UseStartup<Broker.Startup>());
               _disposables.Add(server);
               return server;
          }

          // Aboneaza la categoria "sport" un Receiver real care scrie in journalPath.
          private void Subscribe(TestServer broker, string journalPath)
          {
               var receiver = new TestServer(new WebHostBuilder()
                    .ConfigureServices(services => services.AddSingleton(new NewsJournal(journalPath)))
                    .UseStartup<Receiver.Startup>());
               _disposables.Add(receiver);

               broker.Services.GetRequiredService<IConnectionStorageService>()
                    .Add(new Connection("https://receiver.test", "sport", CreateChannel(receiver)));
          }

          private GrpcChannel CreateChannel(TestServer server)
          {
               var channel = GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions { HttpHandler = server.CreateHandler() });
               _disposables.Add(channel);
               return channel;
          }

          private Publisher.PublisherClient CreatePublisher(TestServer broker) => new(CreateChannel(broker));

          private static PublishRequest CreateRequest(bool isPersistent = false) => new()
          {
               News = new News
               {
                    MessageId = Guid.NewGuid().ToString(),
                    CorrelationId = Guid.NewGuid().ToString(),
                    MessageType = ContractConstants.NewsPublished,
                    SchemaVersion = ContractConstants.SchemaVersion,
                    Category = "sport",
                    Title = "Final score",
                    Content = "2-1",
                    IsPersistent = isPersistent
               }
          };

          private static async Task<NewsStatusReply> WaitForStatusAsync(Publisher.PublisherClient client, string messageId, NewsStatus expected)
          {
               var deadline = DateTime.UtcNow.AddSeconds(10);
               while (true)
               {
                    var reply = await client.GetNewsStatusAsync(new NewsStatusRequest { MessageId = messageId });
                    if (reply.Status == expected || DateTime.UtcNow > deadline)
                    {
                         return reply;
                    }
                    await Task.Delay(50);
               }
          }

          [Fact]
          public async Task Published_news_is_delivered_with_the_same_correlation_id()
          {
               var broker = StartBroker();
               Subscribe(broker, JournalPath);
               var client = CreatePublisher(broker);
               var request = CreateRequest();

               var reply = await client.PublishMessageAsync(request);

               Assert.False(reply.Duplicate);
               Assert.Equal(NewsStatus.Delivered, (await WaitForStatusAsync(client, reply.MessageId, NewsStatus.Delivered)).Status);
               var received = News.Parser.ParseJson(Assert.Single(File.ReadAllLines(JournalPath)));
               Assert.Equal(request.News.MessageId, received.MessageId);
               Assert.Equal(request.News.CorrelationId, received.CorrelationId);
          }

          [Fact]
          public async Task Publish_retried_after_a_lost_reply_is_delivered_only_once()
          {
               var broker = StartBroker();
               Subscribe(broker, JournalPath);
               var client = CreatePublisher(broker);
               var request = CreateRequest();

               // Prima publicare a reusit, dar Sender-ul nu a primit raspunsul si repeta cererea cu acelasi message_id.
               await client.PublishMessageAsync(request);
               var retry = await client.PublishMessageAsync(request);

               Assert.True(retry.Duplicate);
               await WaitForStatusAsync(client, request.News.MessageId, NewsStatus.Delivered);
               await Task.Delay(300);
               Assert.Single(File.ReadAllLines(JournalPath));
          }

          [Fact]
          public async Task Invalid_publish_requests_return_the_documented_error_codes()
          {
               var client = CreatePublisher(StartBroker());

               var missingTitle = CreateRequest();
               missingTitle.News.Title = "";
               var newerSchema = CreateRequest();
               newerSchema.News.SchemaVersion = 2;
               var reused = CreateRequest();
               await client.PublishMessageAsync(reused);
               var reusedWithOtherContent = reused.Clone();
               reusedWithOtherContent.News.Content = "other";

               Assert.Equal(StatusCode.InvalidArgument, (await Assert.ThrowsAsync<RpcException>(() => client.PublishMessageAsync(missingTitle).ResponseAsync)).StatusCode);
               Assert.Equal(StatusCode.FailedPrecondition, (await Assert.ThrowsAsync<RpcException>(() => client.PublishMessageAsync(newerSchema).ResponseAsync)).StatusCode);
               Assert.Equal(StatusCode.AlreadyExists, (await Assert.ThrowsAsync<RpcException>(() => client.PublishMessageAsync(reusedWithOtherContent).ResponseAsync)).StatusCode);
          }

          [Fact]
          public async Task News_that_keeps_failing_at_the_consumer_ends_in_the_dead_letter_file()
          {
               var broker = StartBroker();
               // Jurnalul este un director, deci fiecare scriere esueaza si Receiver-ul raspunde NACK_RETRY.
               var brokenJournal = Directory.CreateDirectory(Path.Combine(_directory, "broken")).FullName;
               Subscribe(broker, brokenJournal);
               var client = CreatePublisher(broker);
               var request = CreateRequest();

               await client.PublishMessageAsync(request);
               var status = await WaitForStatusAsync(client, request.News.MessageId, NewsStatus.DeadLettered);

               Assert.Equal(NewsStatus.DeadLettered, status.Status);
               Assert.Contains("3 failed attempts", status.LastError);
               var entry = DeadLetterEntry.Parser.ParseJson(Assert.Single(File.ReadAllLines(DeadLetterPath)));
               Assert.Equal(request.News.MessageId, entry.News.MessageId);
               Assert.Equal(3, entry.Attempts);
          }

          [Fact]
          public async Task Persistent_news_published_before_a_restart_is_delivered_after_it()
          {
               var before = StartBroker();
               var request = CreateRequest(isPersistent: true);
               await CreatePublisher(before).PublishMessageAsync(request);
               // Oprire fara abonati: stirea este inca nelivrata si ajunge doar in backup.
               await before.Host.StopAsync();

               var after = StartBroker();
               Subscribe(after, JournalPath);
               var status = await WaitForStatusAsync(CreatePublisher(after), request.News.MessageId, NewsStatus.Delivered);

               Assert.Equal(NewsStatus.Delivered, status.Status);
               Assert.Single(File.ReadAllLines(JournalPath));
          }

          [Fact]
          public async Task Health_endpoints_report_live_and_ready()
          {
               var http = StartBroker().CreateClient();

               Assert.Equal("Healthy", await http.GetStringAsync("/health/live"));
               Assert.Equal("Healthy", await http.GetStringAsync("/health/ready"));
          }

          public void Dispose()
          {
               _disposables.Reverse();
               _disposables.ForEach(x => x.Dispose());
               Directory.Delete(_directory, recursive: true);
          }
     }
}
