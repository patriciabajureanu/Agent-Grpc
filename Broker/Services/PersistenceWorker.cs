using Broker.Services.Interfaces;
using Google.Protobuf;
using GrpcAgent;

namespace Broker.Services
{
     // Salveaza periodic pe disc stirile persistente neconsumate si le restaureaza la pornirea Broker-ului.
     public class PersistenceWorker : BackgroundService
     {
          private readonly IMessageStorageService _messageStorage;
          private readonly string _backupPath;
          private readonly TimeSpan _interval;

          public PersistenceWorker(IMessageStorageService messageStorage, IConfiguration configuration)
          {
               _messageStorage = messageStorage;
               _backupPath = configuration["Persistence:BackupPath"] ?? "news-backup.json";
               _interval = TimeSpan.FromSeconds(configuration.GetValue("Persistence:IntervalSeconds", 5));
          }

          public override async Task StartAsync(CancellationToken cancellationToken)
          {
               // Restaurarea are loc inainte de primul backup, altfel backup-ul gol ar suprascrie fisierul.
               await RestoreAsync(cancellationToken);
               await base.StartAsync(cancellationToken);
          }

          protected override async Task ExecuteAsync(CancellationToken stoppingToken)
          {
               using var timer = new PeriodicTimer(_interval);

               try
               {
                    while (await timer.WaitForNextTickAsync(stoppingToken))
                    {
                         await BackupAsync(stoppingToken);
                    }
               }
               catch (OperationCanceledException)
               {
               }

               // Ultimul backup la oprirea normala, ca sa nu se piarda stirile primite dupa ultimul tick.
               await BackupAsync(CancellationToken.None);
          }

          public async Task BackupAsync(CancellationToken cancellationToken)
          {
               var backup = new NewsBackup();
               backup.Items.AddRange(_messageStorage.GetUnconsumedPersistent());

               try
               {
                    // Scriem intr-un fisier temporar si apoi il mutam, ca o cadere in timpul scrierii sa nu strice backup-ul existent.
                    var tempPath = _backupPath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, JsonFormatter.Default.Format(backup), cancellationToken);
                    File.Move(tempPath, _backupPath, overwrite: true);
               }
               catch (Exception exception) when (exception is not OperationCanceledException)
               {
                    Console.WriteLine($"Could not write the backup {_backupPath}. {exception.Message}");
               }
          }

          public async Task RestoreAsync(CancellationToken cancellationToken)
          {
               if (!File.Exists(_backupPath))
               {
                    return;
               }

               try
               {
                    var json = await File.ReadAllTextAsync(_backupPath, cancellationToken);
                    var backup = JsonParser.Default.Parse<NewsBackup>(json);
                    foreach (var news in backup.Items)
                    {
                         _messageStorage.Add(news);
                    }
                    Console.WriteLine($"Restored {backup.Items.Count} persistent news from {_backupPath}");
               }
               catch (Exception exception) when (exception is InvalidProtocolBufferException or InvalidJsonException)
               {
                    // Fisierul stricat este pastrat pentru analiza in loc sa fie suprascris de urmatorul backup.
                    File.Move(_backupPath, _backupPath + ".corrupt", overwrite: true);
                    Console.WriteLine($"Backup {_backupPath} is corrupt and was moved to {_backupPath}.corrupt. {exception.Message}");
               }
          }
     }
}
