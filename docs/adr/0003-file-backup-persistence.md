# ADR 0003: Persistenta prin backup periodic intr-un fisier

## Context

Broker-ul tine stirile in memorie (colectii thread-safe). La o repornire
neasteptata, stirile neconsumate s-ar pierde.

## Decizia

Un worker de fundal (`PersistenceWorker`) scrie la fiecare
`Persistence:IntervalSeconds` (implicit 5 s) stirile persistente nelivrate in
`news-backup.json`, impreuna cu numarul de incercari. Scrierea se face intr-un
fisier temporar, mutat apoi peste backup, ca o cadere in timpul scrierii sa nu
strice backup-ul. La pornire, backup-ul este restaurat inainte ca Broker-ul sa
fie "ready". La oprirea normala se mai face un backup final. Producatorul
alege pentru fiecare stire daca este persistenta (`is_persistent`). Stirile
tranzitorii raman doar in memorie.

## Alternative

- **Baza de date sau broker existent (RabbitMQ, Kafka)**: durabilitate la
  fiecare mesaj, dar introduce o dependenta externa.
- **Scriere pe disc la fiecare mesaj (write-ahead log)**: nu pierde nimic, dar
  fiecare publicare asteapta discul.

## Consecinte

- Publicarea nu asteapta discul, deci raspunsul este rapid.
- Fisierul este JSON lizibil (formatul Protobuf JSON) si poate fi inspectat
  manual.
- Un backup stricat este redenumit `.corrupt` si nu blocheaza pornirea.

## Limita cunoscuta

Stirile persistente primite in ultimele cel mult 5 secunde inainte de o cadere
neasteptata se pot pierde. Abonamentele nu sunt salvate: dupa repornirea
Broker-ului, fiecare Receiver trebuie repornit.
