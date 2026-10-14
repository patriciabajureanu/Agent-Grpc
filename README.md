# Agent-Grpc

Agent de stiri publish/subscribe prin gRPC peste HTTPS, cu cinci proiecte:

- **Broker** primeste stirile si le transmite abonatilor categoriei; stirile
  persistente neconsumate sunt salvate periodic pe disc si restaurate la pornire.
- **Sender** publica stiri (categorie, titlu, continut, persistenta) din consola.
- **Receiver** se aboneaza la o categorie de stiri si afiseaza stirile primite.
- **Common** contine contractele Protobuf (`Common/Protos`) si adresele comune.
- **Broker.Tests** contine testele pentru backup si restaurare.

## Cerinte

- .NET SDK 8.0 sau un SDK mai nou care poate compila `net8.0`.
- Runtime-urile .NET 8 si ASP.NET Core 8 pentru rulare (incluse in SDK 8).
- Git si acces la NuGet pentru restaurarea pachetelor.
- Certificatul HTTPS de dezvoltare .NET, de incredere (o singura data pe calculator):

  ```powershell
  dotnet dev-certs https --trust
  ```
- Optional: Visual Studio 2022 cu suport pentru .NET 8.

## Descarcare si compilare

Versiunea completa cu Receiver se afla pe ramura `main`.

```powershell
git clone --branch main https://github.com/patriciabajureanu/Agent-Grpc.git
cd Agent-Grpc
dotnet restore "Agent Grpc.sln"
dotnet build "Agent Grpc.sln"
dotnet test "Agent Grpc.sln"
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

   Introdu o categorie, de exemplu `sport`. Asteapta `Subscribed reply: True`.
   Apasarea Enter la mesajul de iesire inchide Receiver-ul.

3. Sender:

   ```powershell
   dotnet run --project Sender
   ```

   Introdu aceeasi categorie (`sport`), titlul, continutul si `y` daca stirea
   este persistenta. Sender afiseaza `Publish Reply: True (id ...)`, iar
   Receiver afiseaza `[sport] <titlu> ...` si continutul.
   Livrarea este verificata de Broker la aproximativ doua secunde.

Poti porni mai multe instante Receiver, inclusiv pe categorii diferite.

### Verificarea persistentei

1. Publica o stire persistenta (`y`) intr-o categorie fara abonati, de exemplu `tech`.
2. Asteapta cel putin 5 secunde: stirea apare in `Broker/news-backup.json`.
3. Opreste Broker (chiar fortat, din Task Manager) si porneste-l din nou: in
   consola apare `Restored 1 persistent news from news-backup.json`.
4. Porneste un Receiver pe `tech`: stirea este livrata, apoi dispare din backup.
Pentru oprirea proceselor foloseste Ctrl+C.

## Configurare si limite

- Broker asculta pe `https://localhost:5001`.
- Receiver foloseste un port local liber, atribuit automat, si comunica adresa
  `https://localhost:<port>` catre Broker la abonare. Broker accepta doar adrese HTTPS.
- Adresele sunt definite in `Common/EndpointsConstants.cs`; Broker si Receiver
  folosesc HTTP/2 peste TLS, configurat in fisierele `appsettings.json`.
  Certificatul de dezvoltare este valabil doar pentru `localhost`.
- Configuratia implicita este pentru procese care ruleaza pe acelasi calculator.
  Pentru calculatoare diferite trebuie configurate adrese accesibile intre ele.
- O stire ramane in Broker pana cand este livrata cu succes cel putin unui
  abonat al categoriei; pana atunci este reincercata la fiecare doua secunde.
- Stirile persistente neconsumate sunt salvate la fiecare 5 secunde (si la
  oprirea normala) in `Broker/news-backup.json`, configurabil prin sectiunea
  `Persistence` din `Broker/appsettings.json`. Stirile tranzitorii raman doar in
  memorie si se pierd la oprirea Broker. Un backup stricat este redenumit
  `news-backup.json.corrupt`, iar Broker porneste fara el.
- Livrarea este "cel putin o data": stirile primite in ultimele 5 secunde
  inainte de o cadere pot fi pierdute, iar o stire livrata chiar inainte de
  cadere poate fi livrata din nou dupa repornire.
- Abonamentele sunt pastrate doar in memorie. Dupa repornirea Broker,
  reporneste Receiver pentru a recrea abonamentul.
- Daca Broker nu ruleaza sau portul 5001 este ocupat, rezolva problema inainte
  de abonare/publicare. Un Receiver care nu mai este disponibil este eliminat
  din abonamente cand livrarea esueaza cu `Unavailable`.

## Colaborare

Creeaza o ramura proprie pornind de la `main`, salveaza modificarile
prin commit si deschide un pull request catre `main`. Nu adauga `bin/`, `obj/`, `.vs/`,
fisiere `*.user` sau secrete; acestea sunt excluse prin `.gitignore`.

Workflow-ul GitHub Actions compileaza solutia in Release la push si pull request,
tratand avertismentele de compilare ca erori, si ruleaza testele.
