# Scenarii de esec

Fiecare scenariu este verificat printr-un test automat, rulat cu
`dotnet test "Agent Grpc.sln"`, si prin pasi manuali reproductibili. Extrasele
de log de mai jos provin din rulari reale. Liniile JSON au fost condensate la
timestamp, nivel, EventId, componenta, mesaj si primele 8 caractere din
`CorrelationId`/`MessageId`.

## 1. Raspunsul la publicare se pierde

| | |
|---|---|
| **Stare initiala** | Broker si un Receiver pe `sport` ruleaza. |
| **Injectarea esecului** | Broker-ul accepta stirea, dar raspunsul nu ajunge la Sender (sau Broker-ul este indisponibil); Sender repeta cererea. |
| **Comportament observat** | Repetarea cu acelasi `message_id` primeste `duplicate = true`. Cand Broker-ul lipseste, Sender reincearca de 4 ori cu backoff si jitter, apoi raporteaza esecul. |
| **Mecanism de recuperare** | Chei de idempotenta (`message_id`) si deduplicare in Broker; `GetNewsStatus` permite aflarea rezultatului. |
| **Stare finala** | Stirea exista o singura data in Broker si este livrata o singura data. |
| **Garantie** | O publicare repetata nu produce a doua livrare. |
| **Neacoperit** | Dupa repornirea Broker-ului, fereastra de deduplicare este goala; duplicatul este oprit atunci de jurnalul Receiver-ului (scenariul 3). |

Teste: `IntegrationTests.Publish_retried_after_a_lost_reply_is_delivered_only_once`,
`RetryPolicyTests`.

Reproducere manuala, cu Broker-ul oprit:

```powershell
dotnet run --project Sender 2> sender.log
```

```text
19:17:00.028Z Warn 1502 Sender  Publish attempt 1/4 failed with Unavailable in 4178 ms; retrying in 393 ms  [corr=ef325a23 msg=f57f3b7f]
19:17:00.487Z Warn 1502 Sender  Publish attempt 2/4 failed with Unavailable in 0 ms; retrying in 713 ms     [corr=ef325a23 msg=f57f3b7f]
19:17:05.230Z Warn 1502 Sender  Publish attempt 3/4 failed with Unavailable in 4023 ms; retrying in 1770 ms [corr=ef325a23 msg=f57f3b7f]
19:17:07.013Z Erro 1503 Sender  Publish failed after 4 attempt(s) with Unavailable: Error connecting to subchannel.
```

Toate cele patru incercari au acelasi `msg=f57f3b7f`.

## 2. Broker-ul cade cu stiri nelivrate

| | |
|---|---|
| **Stare initiala** | Broker ruleaza; o stire persistenta `tech` nu are inca abonati. |
| **Injectarea esecului** | Procesul Broker este oprit fortat (`taskkill /F /IM Broker.exe`). |
| **Comportament observat** | La pornire, Broker-ul restaureaza backup-ul inainte sa raporteze `ready`; stirea este livrata primului abonat `tech`. |
| **Mecanism de recuperare** | `PersistenceWorker`: backup la 5 s (fisier temporar + mutare), restaurare la pornire. |
| **Stare finala** | Stirea are starea `Delivered`; backup-ul devine `{ }`. |
| **Garantie** | Stirile persistente nelivrate, salvate in backup, supravietuiesc caderii; numarul de incercari se pastreaza. |
| **Neacoperit** | Stirile din ultimele cel mult 5 s si toate stirile tranzitorii se pierd. Abonamentele trebuie refacute. |

Teste: `IntegrationTests.Persistent_news_published_before_a_restart_is_delivered_after_it`,
`PersistenceWorkerTests`.

```text
# inainte de cadere
19:16:01.686Z Info 1001 PublisherService   News accepted in category tech, persistent True  [corr=87cd724c msg=95880d30]
# dupa taskkill /F si repornire
19:16:23.734Z Info 1303 PersistenceWorker  Restored 1 persistent news from news-backup.json
19:16:30.086Z Info 1101 SubscriberService  Subscriber https://localhost:62191 added to category tech
19:16:34.199Z Info 1201 SenderWorker       Subscriber https://localhost:62191 answered Ack  in 2432 ms  [corr=87cd724c msg=95880d30]
```

`Sender` -> `status` -> `95880d30-...` afiseaza `Status: Delivered, attempts: 0`.

## 3. Receiver-ul cade dupa efect, inainte de ACK

| | |
|---|---|
| **Stare initiala** | Receiver a scris stirea in jurnal (efectul local). |
| **Injectarea esecului** | Procesul cade inainte ca raspunsul ACK sa ajunga la Broker; Broker-ul relivreaza stirea. |
| **Comportament observat** | La repornire, Receiver-ul citeste jurnalul; relivrarea este confirmata cu ACK `duplicate`, fara o a doua linie in jurnal. |
| **Mecanism de recuperare** | Efectul si marcarea "procesat" sunt aceeasi scriere (`NewsJournal`); o linie scrisa partial este ignorata, deci stirea este procesata din nou. |
| **Stare finala** | O singura linie in jurnal; Broker-ul marcheaza stirea `Delivered`. |
| **Garantie** | Efectul este aplicat o singura data, chiar daca stirea este livrata de mai multe ori. |
| **Neacoperit** | Jurnalul este per Receiver; doua instante cu jurnale diferite pe aceeasi categorie primesc fiecare stirea. |

Teste: `NewsJournalTests.Redelivery_after_a_crash_before_ack_is_recognized_as_duplicate`,
`NewsJournalTests.Partially_written_line_is_ignored_and_the_news_is_processed_again`,
`NewsJournalTests.Same_news_produces_the_effect_only_once`.

## 4. Consumatorul esueaza mereu (mesaj neprocesabil)

| | |
|---|---|
| **Stare initiala** | Broker pornit cu `Delivery__MaxAttempts=3`; Receiver cu `RECEIVER_JOURNAL` indreptat spre un director, deci nu poate scrie. |
| **Injectarea esecului** | Se publica o stire `sport`; Receiver raspunde `NACK_RETRY` la fiecare livrare. |
| **Comportament observat** | Broker-ul reincearca cu backoff crescator (527 ms, 1825 ms), apoi muta stirea in dead-letter. |
| **Mecanism de recuperare** | Limita de incercari si `dead-letter.jsonl` cu motivul, numarul de incercari si stirea completa. |
| **Stare finala** | Stirea are starea `DeadLettered`; nu mai este reincercata. |
| **Garantie** | Un mesaj neprocesabil nu este repetat la infinit si nu se pierde. |
| **Neacoperit** | Retrimiterea din dead-letter se face manual (republicare). |

Test: `IntegrationTests.News_that_keeps_failing_at_the_consumer_ends_in_the_dead_letter_file`.

```text
19:17:20.851Z Info 1001 PublisherService  News accepted in category sport, persistent False  [corr=a0815396 msg=4c955a01]
19:17:25.169Z Info 1201 SenderWorker      Subscriber https://localhost:62207 answered NackRetry journal unavailable: Access to the path '<tmp>\broken-journal' is denied. in 2439 ms
19:17:25.172Z Warn 1202 SenderWorker      Delivery attempt 1/3 failed: NackRetry journal unavailable: ...; next attempt in 527 ms
19:17:26.727Z Warn 1202 SenderWorker      Delivery attempt 2/3 failed: NackRetry journal unavailable: ...; next attempt in 1825 ms
19:17:28.732Z Erro 1203 SenderWorker      News moved to dead-letter after 3 attempt(s): 3 failed attempts, last error: NackRetry journal unavailable: ...
```

Linia din `Broker/dead-letter.jsonl`:

```json
{ "news": { "messageId": "4c955a01-9f32-4b02-aab3-15ca5e5fe2d8", "category": "sport", "title": "Stire care nu poate fi procesata", "content": "Continut", "occurredAt": "2026-10-14T19:17:20.850825Z", "correlationId": "a0815396-ca40-4819-b7d2-ee39f79242dd", "messageType": "news.published", "schemaVersion": 1 }, "reason": "3 failed attempts, last error: NackRetry journal unavailable: ...", "attempts": 3, "deadLetteredAt": "2026-10-14T19:17:28.724419800Z" }
```

## Propagarea correlation ID

Aceeasi stire, urmarita in cele trei componente:

```text
19:16:01.663Z Info 1501 Sender               Publish attempt 1 succeeded, duplicate False, in 317 ms  [corr=3878baf1 msg=1081c2f3]
19:16:01.621Z Info 1001 PublisherService     News accepted in category sport, persistent False        [corr=3878baf1 msg=1081c2f3]
19:16:05.563Z Info 1401 NotificationService  News received in category sport, Ack                     [corr=3878baf1 msg=1081c2f3]
19:16:05.607Z Info 1201 SenderWorker         Subscriber https://localhost:62177 answered Ack  in 2437 ms [corr=3878baf1 msg=1081c2f3]
```

Prima livrare catre un abonat nou dureaza aproximativ 2,4 s, pentru ca include
deschiderea conexiunii TLS. Livrarile urmatoare dureaza cateva milisecunde.
