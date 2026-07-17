# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Ce e directorul ăsta

**Nu e un codebase.** E un folder dintr-un vault Obsidian care conține planul de implementare pentru PocketSync — o unealtă personală de transfer fișiere între telefon (Samsung) și PC, pe LAN, fără cloud.

Codul **nu există încă**. Nu există build, teste, lint sau CI, pentru că nu există sursă. Dacă un task pare să ceară rularea codului, întreabă unde a fost creat proiectul — nu presupune că e aici (un proiect Visual Studio în interiorul unui vault Obsidian ar fi o alegere ciudată, dar nu e exclusă).

Utilizatorul lucrează în **Visual Studio, nu din CLI**. Documentele conțin încă comenzi `dotnet new` / `dotnet publish` ca referință conceptuală — tradu-le în pași de VS când răspunzi, nu i le da ca atare.

## Limba

Toate documentele și conversația sunt în **română**. Scrie în română. Termenii tehnici (endpoint, stream, middleware, token) rămân în engleză — așa sunt folosiți deja în docs, nu îi traduce.

## Structura planului

`README.md` e indexul cu roadmap-ul și statusul fazelor. `00-arhitectura.md` explică *de ce*-ul (socket-uri vs HTTP, threat model, de ce C#) și nu conține task-uri. `01`–`05` sunt fazele de implementare. `06-backlog.md` e ce s-a scos intenționat din V1.

Notele sunt legate cu wikilink-uri Obsidian (`[[00-arhitectura#7. Threat model]]`), inclusiv către heading-uri. **Dacă redenumești un heading, verifică ce linkuri îl țintesc** — sunt multe referințe încrucișate între faze, iar Obsidian nu le repară singur.

## Convenții pentru documentele de plan

Astea sunt cerute explicit de utilizator, nu sunt stil accidental:

- **Task-uri cu checkbox, niciodată estimări de timp.** Fără "într-un weekend", "2 zile". Ordinea contează, durata nu.
- **Explicațiile se marchează cu 📖** și stau lipite de task-ul de care aparțin, nu într-o secțiune teoretică separată. Scopul declarat al utilizatorului e să *înțeleagă*, nu doar să livreze — explică mecanismul, nu doar soluția.
- Fiecare fază se termină cu **✅ Definition of done** și un link către următoarea.

## Decizii luate — nu le relitiga

Fiecare a fost luată conștient, contra unei alternative rezonabile. Dacă apare tentația de a le inversa, motivul e în `00-arhitectura.md`.

- **HTTP peste Kestrel, nu socket-uri brute.** Utilizatorul a întrebat explicit despre socketing; răspunsul e că Kestrel *e* codul de socket. Nu propune `TcpListener`.
- **Web UI servit de PC în Fazele 1–3; MAUI abia în Faza 5.** Chrome-ul de pe telefon e deja un client HTTP bun. Aplicația nativă se justifică *doar* prin share sheet-ul Android — nu prin viteză sau "comunicare".
- **.NET 10** (LTS curent). .NET 8 iese din suport în noiembrie 2026.
- **Fără Docker.** Containerele Windows n-au GUI (imposibil pentru tray-ul din Faza 4) și rețeaua izolată ar strica exact descoperirea în LAN. Distribuția se face prin self-contained publish + Inno Setup.
- **Fără HTTPS în V1.** Self-signed e o capcană pe Android. E datorie tehnică *asumată și notată*, nu uitată — soluția reală e Tailscale (`06-backlog.md`).
- **Shutdown/restart sunt în backlog, nu în V1.** Nu e transfer de fișiere; e suprafață de atac separată.

## Regula de disciplină a fazelor

**Nu se trece la faza următoare până nu merge cea curentă pe telefonul fizic** — nu pe localhost, nu pe emulator.

Motivul e că aproape toate problemele reale ale proiectului apar doar între două device-uri fizice: firewall, `0.0.0.0` vs `localhost`, cleartext HTTP pe Android, fișiere mari. Localhost merge întotdeauna, și exact de asta minte.

Dacă utilizatorul vrea să adauge funcționalitate înainte ca faza curentă să fie bifată, redirecționează spre `06-backlog.md`.

## Capcanele cunoscute (deja documentate, nu le rediagnostica de la zero)

Când utilizatorul raportează că "nu merge", verifică întâi lista asta — sunt în docs cu soluție:

| Simptom | Cauză | Unde |
|---|---|---|
| Timeout de pe telefon | Windows Firewall, sau rețea marcată Public | `01` Task 4 |
| Telefonul nu vede serverul deloc | ascultă pe `localhost`, nu `0.0.0.0`; sau rulează sub IIS Express în loc de Kestrel | `01` Task 3 |
| Upload mare crapă / mănâncă RAM | `IFormFile` buffer-ează; trebuie `MultipartReader` | `01` Task 7 |
| Merge în Chrome dar nu din app-ul MAUI | cleartext HTTP blocat din Android 9 | `05` Task 2 |
| QR-ul are IP greșit | adaptoare virtuale (WSL/VirtualBox/VPN) în `GetAllNetworkInterfaces` | `03` Task 5 |
