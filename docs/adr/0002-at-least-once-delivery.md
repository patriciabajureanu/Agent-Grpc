# ADR 0002: Livrare "cel putin o data" cu chei de idempotenta

Aceasta este decizia principala a sistemului.

## Context

Intre Sender, Broker si Receiver se pot pierde cereri si raspunsuri, iar orice
proces poate cadea. Doua situatii nu pot fi distinse de partea care asteapta:

1. Sender nu primeste raspunsul la publicare: nu stie daca Broker-ul a
   acceptat stirea.
2. Receiver cade dupa ce a aplicat efectul, dar inainte de ACK: Broker-ul nu
   stie ca stirea a fost procesata.

## Decizia

Livrarea este **cel putin o data** (at-least-once), iar duplicatele sunt
neutralizate prin idempotenta:

- Sender genereaza `message_id` o singura data si il pastreaza la fiecare
  reincercare (maximum 4, cu backoff exponential si jitter, doar pentru
  `UNAVAILABLE`, `DEADLINE_EXCEEDED` si `RESOURCE_EXHAUSTED`).
- Broker deduplica dupa `message_id`: o repetare intoarce `duplicate = true`,
  iar starea poate fi citita cu `GetNewsStatus`.
- Broker pastreaza stirea pana la ACK si reincearca `NACK_RETRY` si erorile RPC
  de cel mult `Delivery:MaxAttempts` ori, apoi o muta in `dead-letter.jsonl`.
  `NACK_REJECT` merge direct in dead-letter.
- Receiver scrie efectul si id-ul procesat in aceeasi linie din jurnal. O
  relivrare dupa cadere este recunoscuta si confirmata fara un al doilea efect.

## Alternative

- **Cel mult o data**: fara reincercari. Este simplu, dar orice cadere
  pierde stiri.
- **Exact o data**: ar cere o tranzactie distribuita intre Broker si Receiver
  (de exemplu two-phase commit). Este prea complex si fragil pentru acest
  sistem. Efectul observabil "o singura data" se obtine aici din at-least-once
  plus idempotenta la consumator.

## Consecinte

- Nicio reincercare nu este infinita. O stire neprocesabila ajunge in
  dead-letter si poate fi analizata manual.
- Consumatorii trebuie sa fie idempotenti; contractul o cere explicit
  (`notify.proto`).

## Limita cunoscuta

- Fereastra de deduplicare a Broker-ului este doar in memorie (o ora). Dupa
  repornirea Broker-ului, o publicare repetata poate fi acceptata a doua oara.
  Duplicatul este oprit atunci de jurnalul Receiver-ului.
- O stire este considerata livrata cand cel putin un abonat al categoriei a
  confirmat-o. Un al doilea abonat care esueaza temporar in acelasi moment o
  pierde.
- Ordinea nu este garantata dupa reincercari; `occurred_at` permite
  consumatorului sa sorteze.
