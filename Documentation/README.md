# PocketSync

Transfer de fișiere între telefon și PC, în rețeaua locală, fără cloud și fără cablu.

## Ideea în trei propoziții

PC-ul rulează un server HTTP local. Telefonul e client și vorbește cu el prin rețeaua WiFi de acasă. Nimic nu iese din LAN — fără cont, fără server extern, fără Google Drive la mijloc.

## Roadmap

Fazele sunt ordonate ca **felii verticale**: fiecare fază se termină cu ceva ce poți folosi efectiv, nu cu un strat pe jumătate făcut care așteaptă alt strat. Nu sunt termene, doar ordine.

| Fază | Ce ai la final | Status |
|---|---|---|
| [[00-arhitectura]] | Înțelegi cum și de ce | 📖 citește întâi |
| [[01-faza-1-telefon-catre-pc]] | Trimiți un fișier de pe telefon pe PC, din browser | ⬜ |
| [[02-faza-2-pc-catre-telefon]] | Vezi fișierele de pe PC și le descarci pe telefon | ⬜ |
| [[03-faza-3-securitate-si-pairing]] | Token + QR code, nu mai intră oricine de pe WiFi | ⬜ |
| [[04-faza-4-tray-app]] | Aplicație reală cu icon în tray, pornește cu Windows | ⬜ |
| [[05-faza-5-app-android]] | Aplicație nativă cu share sheet | ⬜ |
| [[06-backlog]] | iOS, comenzi PC, acces din afara casei, etc. | 💭 idei |

## Regula de bază a proiectului

**Nu treci la faza următoare până nu merge cea curentă pe telefonul real.** Nu pe localhost, nu pe emulator — pe Samsung, prin WiFi.

Motivul: aproape toate problemele reale ale acestui proiect (firewall, IP-uri, cleartext HTTP, fișiere mari) apar *doar* când sunt două device-uri fizice diferite. Pe localhost totul merge întotdeauna, și exact de asta localhost te minte.

## Stack

- **.NET 10** (LTS curent) peste tot
- **ASP.NET Core Minimal API** — serverul de pe PC
- **HTML + JS vanilla** — UI-ul de pe telefon în Faza 1-3 (fără framework, nu îți trebuie)
- **WPF** — tray app-ul din Faza 4
- **.NET MAUI** — aplicația Android din Faza 5

De ce C#: un singur limbaj pentru server + UI PC + mobil. Vezi [[00-arhitectura#De ce C#]] pentru comparația cu Python/C++.
