# Arhitectură — cum funcționează de fapt

Documentul ăsta nu conține task-uri. E aici ca să înțelegi *de ce* arată planul așa. Citește-l o dată acum, și întoarce-te la el când te blochezi.

---

## 1. Stratul de dedesubt: socket-uri

Ai intuit corect — la bază e socketing. Orice comunicare în rețea e.

Un **socket TCP** e un handle dat de sistemul de operare către o conexiune între două perechi `IP:port`. Îți garantează exact un lucru:

> un flux ordonat de bytes, fără pierderi, între două capete

Atât. Și aici e toată lecția, pentru că ce **nu** îți dă e enorm:

- Nu are noțiunea de "fișier". Trimiți 10 MB, celălalt capăt îi primește în bucăți arbitrare — poate 1400 de bytes, poate 64 KB. Nu ai control asupra bucăților.
- Nu știe cum se numește fișierul.
- Nu știe ce mărime are.
- Nu știe unde se termină un fișier și începe altul.
- Nu știe dacă transferul s-a *terminat* sau doar s-a *blocat* rețeaua.

Toate astea trebuie inventate deasupra socket-ului. Convenția pe care o inventezi se numește **protocol**. De exemplu: *"primii 4 bytes sunt lungimea numelui, urmează numele, urmează 8 bytes cu mărimea fișierului, apoi conținutul brut"*.

Poți scrie asta singur cu `TcpListener` / `TcpClient`. **Dar nu ar trebui.**

## 2. De ce HTTP și nu socket-uri brute

HTTP e un protocol pe care l-a inventat deja altcineva, îl implementează absolut orice platformă, și rezolvă fix problemele de mai sus:

| Problema din socket brut | Ce îți dă HTTP |
|---|---|
| Unde se termină fișierul? | `Content-Length` |
| Cum se numește? Ce tip e? | Headere (`Content-Disposition`, `Content-Type`) |
| Mai multe fișiere odată? | `multipart/form-data` |
| S-a rupt la 90%, reiau de acolo? | `Range` requests |
| Cum debug-ez? | curl, Postman, DevTools |
| Cum îl deschid de pe telefon? | **Orice browser** |

Ultimul rând e cel mai important și e motivul pentru care planul arată așa — vezi secțiunea 5.

Alternativa "protocol propriu peste TCP" e legitimă pentru proiecte ca Syncthing, care au nevoi pe care tu nu le ai (sincronizare continuă, mii de fișiere, topologie mesh). LocalSend — care e cel mai apropiat de ce vrei tu — folosește **HTTP**. Ăsta e un semnal bun.

## 3. Unde intră ASP.NET Core

Când folosești ASP.NET Core, **Kestrel** (serverul web din spate) *este* codul de socket. El face `TcpListener`, acceptă conexiuni, parsează HTTP-ul, și îți dă în mână un `HttpContext` curat.

Stiva completă, de jos în sus:

```
WiFi / Ethernet          ← hardware
   IP                    ← "care device" (192.168.1.42)
   TCP                   ← "care aplicație" (portul 5000) + flux de bytes garantat
   HTTP                  ← mesaje cu headere și body   ← Kestrel se ocupă de tot ce e până aici
   API-ul tău            ← /api/files/upload           ← tu scrii de aici în sus
```

Deci răspunsul la întrebarea ta — *"trebuie aplicația să facă un server API pe PC și prin ăla să comunice?"* — e **da, exact așa**.

## 4. Cine e client și cine e server

Confuzie frecventă, merită lămurită din start:

> **Client/server nu înseamnă cine trimite datele. Înseamnă cine ascultă.**

PC-ul e **server** pentru că el stă și ascultă pe un port, permanent. Telefonul e **client** în ambele direcții — și când face upload (deci *trimite* date), și când face download.

De ce PC-ul e serverul, și nu invers:
- are IP relativ stabil în LAN
- are disc și stă pornit
- nu îl omoară sistemul de operare când ecranul se stinge (Android chiar face asta)

## 5. De ce browserul, și nu aplicație mobilă, în Faza 1

Serverul de pe PC vorbește HTTP. **Telefonul are deja instalat un client HTTP excelent: Chrome.**

Deci în Faza 1 servești o pagină HTML simplă din ASP.NET Core, deschizi pe telefon `http://192.168.1.42:5000`, apeși pe un `<input type="file">`, și fișierul ajunge pe PC. Zero cod mobil, zero MAUI, zero emulator, zero permisiuni Android. Merge și pe iPhone din prima, fără Mac și fără 99$/an.

Bonus mare: **QR-ul de pairing funcționează mai bine așa.** Nu îl scanezi cu aplicația ta (care nu există) — QR-ul conține direct `http://192.168.1.42:5000/?token=abc123`, îl scanezi cu aplicația de cameră a telefonului, se deschide browserul deja autentificat. Pairing rezolvat fără o linie de cod mobil.

Aplicația nativă (Faza 5) rămâne justificată, dar hai să fim cinstiți despre ce cumpără de fapt:

- ✅ **share sheet** — "Share → PocketSync" din orice aplicație. Ăsta e argumentul real.
- ✅ transfer în background (browserul se oprește când schimbi tab-ul)
- ✅ acces mai bun la file system
- ❌ **NU** "comunicarea" — aia merge deja prin browser

## 6. De ce C#

- **Python** — FastAPI ți-ar face serverul mai repede și e mai clar pedagogic, dar povestea pe mobil e proastă (Kivy/BeeWare sunt dureroase) și nu livrezi ușor un `.exe` cu tray icon.
- **C++** — zero motive. Te-ai lupta cu build system, dependințe și memory management pentru exact același rezultat.
- **C#** — un singur limbaj pentru server + UI PC + mobil. ASP.NET Core e genuinely first-class.

Singura verigă slabă e **MAUI**, și doar în Faza 5. Dacă acolo te enervează prea tare, escape hatch-ul e Kotlin + Android Studio (tooling mai bun, dar al doilea limbaj). Nu e o decizie de luat acum.

## 7. Threat model — de la cine te aperi

Important, pentru că determină cât efort pui în securitate:

**De la cine te aperi:** de la orice alt device din rețeaua ta WiFi (vizitatori, dispozitive IoT compromise, vecinul care are parola). Fără token, oricine e pe WiFi poate lista și descărca fișierele de pe PC-ul tău — pentru că serverul tău ascultă pe toată rețeaua.

**De la cine NU te aperi (în V1):** de la cineva care deja *ascultă* traficul din LAN-ul tău. Faza 3 folosește HTTP simplu + token, deci tokenul trece în clar. Pentru uz personal acasă e acceptabil. Pentru distribuit altor oameni, nu.

De ce nu HTTPS în V1: certificat self-signed → browserul de pe telefon urlă cu warning, Android nu îl acceptă ușor, iar tu petreci trei zile pe trust store în loc să transferi fișiere. Vezi [[06-backlog#HTTPS ca lumea]] pentru cum se rezolvă corect mai târziu.

## 8. Cele trei chestii care te vor bloca

Le zic din start ca să le recunoști când apar, nu să pierzi ore crezând că ai greșit codul:

**Windows Firewall.** Prima ta încercare de pe telefon va da timeout. Codul e corect. Firewall-ul blochează conexiunile inbound. → [[01-faza-1-telefon-catre-pc#Task 4]]

**`localhost` vs `0.0.0.0`.** Dacă serverul ascultă pe `localhost`, e vizibil *doar* de pe PC. Telefonul nu îl vede niciodată, oricât te-ai chinui. → [[01-faza-1-telefon-catre-pc#Task 3]]

**Fișierele mari mănâncă RAM.** ASP.NET Core buffer-ează `multipart/form-data` înainte să îți dea controlul. Un mp4 de 4 GB doare. → [[01-faza-1-telefon-catre-pc#Task 7]]
