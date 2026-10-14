# Agent-Grpc

Aplicatie publish/subscribe prin gRPC, cu patru proiecte:

- **Broker** primeste mesajele si le transmite abonatilor topicului.
- **Sender** publica mesaje introduse in consola.
- **Receiver** se aboneaza la un topic si afiseaza mesajele primite.
- **Common** contine contractele Protobuf si adresele comune.

## Cerinte

- .NET SDK 8.0 sau un SDK mai nou care poate compila `net8.0`.
- Runtime-urile .NET 8 si ASP.NET Core 8 pentru rulare (incluse in SDK 8).
- Git si acces la NuGet pentru restaurarea pachetelor.
- Optional: Visual Studio 2022 cu suport pentru .NET 8.

## Descarcare si compilare

Versiunea completa cu Receiver se afla pe ramura `main`.

```powershell
git clone --branch main https://github.com/patriciabajureanu/Agent-Grpc.git
cd Agent-Grpc
dotnet restore "Agent Grpc.sln"
dotnet build "Agent Grpc.sln"
```

Pentru o copie existenta, salveaza modificarile proprii inainte de schimbarea
ramurii, apoi foloseste `git fetch origin`, `git switch main` si
`git pull --ff-only`.

## Rulare locala

Deschide trei terminale in directorul repository-ului. Porneste proiectele in
ordinea urmatoare si lasa fiecare terminal deschis:

1. Broker:

   ```powershell
   dotnet run --project Broker --no-launch-profile
   ```

2. Receiver:

   ```powershell
   dotnet run --project Receiver --no-launch-profile
   ```

   Introdu un topic, de exemplu `test`. Asteapta `Subscribed reply: True`.
   Apasarea Enter la mesajul de iesire inchide Receiver-ul.

3. Sender:

   ```powershell
   dotnet run --project Sender
   ```

   Introdu acelasi topic (`test`) si continutul mesajului. Sender afiseaza
   `Publish Reply: True`, iar Receiver afiseaza `Received: <continut>`.
   Livrarea este verificata de Broker la aproximativ doua secunde.

Poti porni mai multe instante Receiver, inclusiv pe topicuri diferite.
Pentru oprirea proceselor foloseste Ctrl+C.

## Configurare si limite

- Broker asculta pe `http://127.0.0.1:5001`.
- Receiver foloseste un port local liber, atribuit automat, si comunica adresa
  catre Broker la abonare.
- Adresele sunt definite in `Common/EndpointsConstants.cs`; Broker si Receiver
  folosesc HTTP/2, configurat in fisierele `appsettings.json`.
- Configuratia implicita este pentru procese care ruleaza pe acelasi calculator.
  Pentru calculatoare diferite trebuie configurate adrese accesibile intre ele.
- Mesajele si abonamentele sunt pastrate in memorie si se pierd la oprirea Broker.
  Aplicatia nu garanteaza retransmiterea mesajelor ratate. Porneste si aboneaza
  Receiver inainte de publicare. Dupa repornirea Broker, reporneste Receiver
  pentru a recrea abonamentul.
- Daca Broker nu ruleaza sau portul 5001 este ocupat, rezolva problema inainte
  de abonare/publicare. Un Receiver care nu mai este disponibil este eliminat
  din abonamente cand livrarea esueaza cu `Unavailable`.

## Colaborare

Creeaza o ramura proprie pornind de la `main`, salveaza modificarile
prin commit si deschide un pull request catre `main`. Nu adauga `bin/`, `obj/`, `.vs/`,
fisiere `*.user` sau secrete; acestea sunt excluse prin `.gitignore`.

Workflow-ul GitHub Actions compileaza solutia in Release la push si pull request,
tratand avertismentele de compilare ca erori.
