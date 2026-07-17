# Backlog — după V1

Idei care **nu** intră în fazele 1-5. Sunt aici ca să nu le pierzi, și ca să nu le faci prea devreme.

Regula: nimic de aici nu se atinge până [[05-faza-5-app-android]] nu e gata (sau conștient sărită).

---

## iPhone

**Vestea bună:** merge deja. Web UI-ul din Fazele 1-3 funcționează în Safari pe iPhone, fără să faci nimic. Ăsta a fost unul dintre argumentele pentru care planul arată așa.

**Aplicație nativă iOS:** MAUI compilează același cod, dar:
- îți trebuie **Mac** pentru build și semnare (Apple obligă, nu există workaround legal)
- pentru instalat pe telefonul tău: cont developer gratuit, dar build-ul **expiră în 7 zile** și trebuie reinstalat
- pentru altcineva: TestFlight → **99$/an**
- `NSLocalNetworkUsageDescription` în `Info.plist`, plus un dialog de permisiune pentru rețeaua locală care apare doar pe iOS

**Verdict:** dacă ai iPhone, folosește web UI-ul. Aplicația nativă costă un Mac + 99$/an pentru share sheet. Raportul e prost.

---

## Comenzi PC (shutdown / restart)

Era în planul inițial. **Am scos-o intenționat.**

Nu pentru că e o idee proastă — e chiar utilă. Dar:
- nu e transfer de fișiere, e o funcție complet separată lipită pe același proiect
- e o suprafață de atac reală: un endpoint autentificat care oprește PC-ul
- de fiecare dată când te apuci de ea înainte să meargă transferul, amâni lucrul care contează

Când o faci:

- [ ] **Whitelist strict.** Niciodată string de shell venit de la client. Trei acțiuni fixe, hardcodate: shutdown, restart, sleep. Un enum, nu un string.
- [ ] Endpoint separat (`/api/power/*`), cu drum diferit în UI de fișiere
- [ ] Confirmare obligatorie în UI (nu vrei shutdown din degete groase)
- [ ] **La testare, `shutdown /s /t 60`**, ca să poți `shutdown /a` când (nu dacă) greșești. Cu `/t 0` la primul test îți închizi PC-ul cu VS-ul nesalvat.
- [ ] Ecran separat în aplicație, departe de butonul de upload

📖 De ce whitelist și nu "doar validez input-ul": orice cale prin care un string de la client ajunge la `Process.Start` e RCE dacă validarea are o gaură. Un enum nu are gaură — nu există input care să producă altceva decât cele trei valori. **Design-ul bate validarea.**

---

## HTTPS ca lumea

Datoria tehnică asumată la [[03-faza-3-securitate-si-pairing#⚠️ Ce NU faci în faza asta HTTPS]]. Devine obligatorie dacă dai aplicația altcuiva sau ieși din LAN.

Opțiuni, de la rău la bine:

**Self-signed** — ce evitam. Warning în browser, Android nu îl acceptă din 7 încoace fără config per-aplicație. Nu merită.

**mkcert / CA local** — generezi propria autoritate și o instalezi pe fiecare device. Merge, dar cere pași manuali pe fiecare telefon. Nu poți da asta altcuiva.

**Tailscale** — vezi mai jos. Rezolvă și HTTPS, și accesul de la distanță, dintr-o mișcare.

**Verdict:** nu face HTTPS izolat. Fă Tailscale, și primești HTTPS pe deasupra.

---

## Acces din afara casei

Scenariul: ești la muncă, vrei un fișier de pe PC-ul de acasă.

**NU: port forwarding pe router.** Îți expui serverul pe internetul deschis, cu tot cu botnet-urile care scanează porturi 24/7. Un bug în validarea ta devine o breșă reală. Nu face asta.

**DA: Tailscale.** VPN mesh peste WireGuard. Instalezi pe PC și pe telefon, ambele primesc un IP privat stabil (`100.x.x.x`) care merge de oriunde. Serverul tău nu se schimbă deloc — doar că `192.168.1.42` devine `100.x.x.x`, iar IP-ul ăla nu se mai schimbă la fiecare reboot de router.

Bonus: **Tailscale Serve** îți dă certificat TLS valid automat, deci rezolvă și HTTPS. Gratuit pentru uz personal.

**De ce e curat:** conexiunea e criptată end-to-end de WireGuard, nu de tine. Nimic nu e expus pe internet. Tu nu ai scris niciun cod de securitate. Cel mai bun cod de criptografie e cel pe care nu îl scrii.

---

## Resume la transfer întrerupt

WiFi pică la 90% dintr-un mp4 de 4 GB → o iei de la zero. Enervant.

**Download** e ușor: HTTP `Range` requests. ASP.NET Core îl suportă deja dacă folosești `Results.File` — clientul cere `Range: bytes=3900000000-` și tu servești de acolo. Browserul o face automat.

**Upload** e mai greu — HTTP nu are un standard pentru asta. Fie inventezi ceva (chunk-uri numerotate + endpoint care întreabă "cât ai primit?"), fie te uiți la **tus.io**, un protocol de resumable upload peste HTTP, cu implementare .NET gata făcută.

---

## Descoperire automată (mDNS)

Ca să nu mai scanezi QR / tastezi IP: PC-ul se anunță în LAN ca `pocketsync.local`, telefonul îl găsește singur.

Librărie: `Makaretu.Dns.Multicast` sau `Zeroconf`.

**Realitate:** mDNS pe Android e istoric problematic (nu merge când ecranul e stins, unele routere blochează multicast). E "nice to have", nu "fixes a real problem" — QR-ul îl scanezi o singură dată. Prioritate mică.

---

## Sincronizare automată de foldere

"Folderul ăsta de pe telefon să fie mereu la fel cu ăsta de pe PC."

**Atenție: asta e un proiect complet diferit, nu o funcție în plus.** Îți trebuie: detecție de schimbări pe ambele capete, rezolvare de conflicte (fișierul s-a schimbat în ambele părți — cine câștigă?), ștergeri (s-a șters, sau doar nu l-am văzut?), tombstones, și un model de consistență.

Syncthing face fix asta și are ani de dezvoltare în spate.

**Recomandare:** dacă chiar vrei sincronizare, instalează Syncthing. Dacă vrei să *înveți* cum se face, e un proiect nou, nu o fază din ăsta.

---

## Multi-PC / multi-telefon

Momentan: un token, un server. Pentru mai multe device-uri:
- token per device, nu global (poți revoca un singur telefon)
- nume pentru device-uri ("Samsung S22", "Laptop muncă")
- UI care arată ce e pairat, cu buton de revocare individual

Nu e greu, dar e inutil până nu ai al doilea device.

---

## Idei mai mici

- **Clipboard sync** — copiezi pe telefon, apeși pe PC. Surprinzător de util, surprinzător de ușor (un endpoint și `Clipboard.SetText`).
- **Trimite link** — deschide un URL de pe telefon direct în browserul de pe PC.
- **Miniaturi** pentru poze în listă (`GET /api/files/{id}/thumbnail`)
- **Compresie** pentru foldere întregi (zip on the fly, streaming)
- **Wake-on-LAN** ca să pornești PC-ul de pe telefon (ironic complementar cu shutdown)
