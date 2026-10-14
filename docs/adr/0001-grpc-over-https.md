# ADR 0001: gRPC cu Protobuf peste HTTPS

## Context

Componentele (Sender, Broker, Receiver) ruleaza in procese separate si comunica
prin retea. Varianta initiala folosea HTTP/2 necriptat (h2c), activat prin
switch-ul `Http2UnencryptedSupport`. Datele circulau in clar, iar contractul
nu avea versiune.

## Decizia

Comunicarea foloseste gRPC peste HTTPS (TLS), cu certificatul de dezvoltare
.NET. Contractele sunt fisiere `.proto` in `Common/Protos`, in pachetul
versionat `agent.v1`. Kestrel accepta HTTP/1.1 si HTTP/2; gRPC negociaza
HTTP/2 prin ALPN, iar endpoint-urile de health pot fi apelate si din browser
sau `curl`.

## Alternative

- **Socket-uri TCP cu JSON** (Partea 1 a lucrarii): control complet, dar
  framing, serializare, deadline-uri si erori trebuie scrise manual.
- **REST/JSON peste HTTPS**: usor de inspectat, dar fara contract tipizat si
  cu serializare mai lenta.
- **gRPC peste h2c**: mai simplu local, dar fara criptare si nepotrivit in
  afara calculatorului de dezvoltare.

## Consecinte

- Codul client si server este generat din contract, iar incompatibilitatile
  apar la compilare.
- Deadline-ul, anularea si codurile de eroare (`StatusCode`) vin din gRPC.
- Fiecare calculator are nevoie de un certificat de incredere
  (`dotnet dev-certs https --trust`).

## Limita cunoscuta

Certificatul de dezvoltare este emis doar pentru `localhost`. Pentru
calculatoare diferite este nevoie de un certificat emis pentru numele lor.
