# Faza 5 — Aplicație Android (MAUI)

**Obiectiv:** "Share → PocketSync" din orice aplicație de pe telefon.

**Prerequisite:** [[04-faza-4-tray-app]] complet. Serios — dacă începi faza asta cu backend-ul instabil, nu vei ști niciodată dacă problema e MAUI sau serverul.

---

## Citește asta înainte să începi

Web UI-ul tău merge deja. Faza asta **nu** aduce funcționalitate nouă de transfer. Adaugă:

- ✅ **Share sheet** — trimiți din Galerie/WhatsApp/orice, fără să deschizi browserul. Ăsta e singurul motiv real.
- ✅ Transfer în background (browserul îl oprește când schimbi tab-ul)
- ✅ Iconiță în drawer, se simte ca aplicație
- ❌ NU: viteză mai bună, comunicare "adevărată", sau ceva ce browserul nu poate

**Dacă share sheet-ul nu te încântă suficient, sari peste faza asta.** Nu e o înfrângere — e recunoașterea că unealta ta e deja gata. Mergi direct la [[06-backlog]].

Serverul nu se schimbă deloc aici. MAUI e doar un alt client HTTP care lovește aceleași endpoint-uri pe care le testezi cu curl de trei faze.

---

## Task 1 — Proiect gol pe device-ul real

```
dotnet new maui -n PocketSync.Mobile
```

- [ ] Rulează pe **Samsung**, prin USB debugging — nu pe emulator
- [ ] Vezi ecranul default

📖 **De ce nu emulatorul:** emulatorul e pe o rețea virtuală. `192.168.1.42` de pe emulator nu e PC-ul tău. Vei debug-a probleme care nu există și vei rata probleme care există. Toate lucrurile care te vor durea aici — permisiuni, cleartext HTTP, storage, share sheet — se comportă altfel pe device real. **Telefonul, cablul, gata.**

---

## Task 2 — Cleartext HTTP

Din Android 9, HTTP simplu e blocat by default în aplicații. Request-ul tău va eșua cu o eroare care nu spune deloc asta.

În `Platforms/Android/AndroidManifest.xml`:

```xml
<application android:usesCleartextTraffic="true" ...>
```

- [ ] Adăugat
- [ ] `GET /api/status` funcționează din aplicație

📖 **De ce Chrome mergea și aplicația nu:** browserul te lăsa (cu un badge "Not secure" în bara de adrese). Aplicațiile native primesc default-ul strict. E consecința directă a deciziei de la [[03-faza-3-securitate-si-pairing#⚠️ Ce NU faci în faza asta HTTPS]] — datoria tehnică pe care ai notat-o îți prezintă nota de plată exact aici.

📖 Varianta mai curată e `networkSecurityConfig` care permite cleartext doar pentru IP-uri din LAN (`192.168.*`, `10.*`) în loc de global. Mai mult XML, dar e postura corectă.

---

## Task 3 — `ApiService`

- [ ] `HttpClient` **singleton**, injectat prin DI
- [ ] Header `X-Auth-Token` la fiecare request (mecanismul de la [[03-faza-3-securitate-si-pairing#Task 4]] — aici cookie-ul nu ajută, ești non-browser)
- [ ] Metode: `GetStatus()`, `ListFiles()`, `Upload()`, `Download()`
- [ ] Timeout rezonabil (5s pentru status, **infinit** pentru transfer)

📖 **`HttpClient` singleton, nu `new` per request.** Fiecare `new HttpClient()` deschide propriul pool de conexiuni și nu îl eliberează imediat — nimerești în socket exhaustion. E una dintre cele mai frecvente greșeli în .NET, și e cu atât mai perfidă cu cât merge perfect în teste și crapă în producție.

📖 **Timeout infinit la transfer:** default-ul de 100s omoară un mp4 de 4 GB pe WiFi lent. Timeout-ul ar trebui să prindă *"nu răspunde"*, nu *"durează mult"* — sunt lucruri diferite.

---

## Task 4 — Pairing prin QR

NuGet: `ZXing.Net.Maui`

- [ ] Permisiune de cameră (`AndroidManifest.xml` + cerere runtime)
- [ ] Scanezi QR-ul din tray app
- [ ] **Parsezi ca URL** (`Uri` + query string), nu ca `{ip}:{port}:{token}`
- [ ] Salvezi în `SecureStorage`, nu în `Preferences`
- [ ] Test `GET /api/status` → dacă merge, la MainPage

📖 **De ce același format de QR ca la browser:** dacă QR-ul rămâne un URL, funcționează în ambele lumi — scanat cu camera deschide browserul, scanat cu aplicația ta o configurează. Un singur QR, doi consumatori, zero cod în plus pe server. Dacă ai fi inventat un format propriu (`ps://ip:port:token`), ai fi avut nevoie de două QR-uri sau ai fi rupt fluxul din browser.

📖 **`SecureStorage` vs `Preferences`:** `Preferences` = SharedPreferences, plaintext, citibil pe un telefon cu root. `SecureStorage` merge prin Android Keystore, cu cheie ținută în hardware. E echivalentul DPAPI de la [[03-faza-3-securitate-si-pairing#Task 2]] — aceeași idee, alt sistem de operare.

---

## Task 5 — Ecranul principal

- [ ] Status conexiune sus (verde/roșu)
- [ ] Listă fișiere de pe PC (`GET /api/files`)
- [ ] Tap → download, cu progres
- [ ] Buton "+" → upload
- [ ] Pull-to-refresh

---

## Task 6 — Upload cu progres

- [ ] `FilePicker` pentru selectare
- [ ] `MultipartFormDataContent` + `StreamContent` (**nu** `ByteArrayContent`)
- [ ] Bară de progres reală
- [ ] Gestionezi eșecul (WiFi pică la 80%) — mesaj clar, buton de retry

📖 **`StreamContent`, nu `ByteArrayContent`:** `ByteArrayContent` cere tot fișierul în RAM-ul telefonului. Android omoară aplicația la 4 GB fără menajamente. Exact aceeași lecție ca la [[01-faza-1-telefon-catre-pc#Task 7]], în oglindă: pe server nu voiai să *primești* tot în RAM, aici nu vrei să *trimiți* din RAM. Stream = "dă-mi bucăți pe măsură ce merg".

📖 Progresul cu `HttpClient` nu e gratuit ca la `XMLHttpRequest` — îți trebuie un `Stream` custom care raportează cât s-a citit. ~30 de linii. E un exercițiu bun: te obligă să vezi că un `Stream` e doar o interfață, și că poți pune orice în spatele ei.

---

## Task 7 — Share sheet ← **motivul întregii faze**

În `Platforms/Android/MainActivity.cs`, `IntentFilter` pentru `ACTION_SEND` și `ACTION_SEND_MULTIPLE`, cu `DataMimeType = "*/*"`.

- [ ] Aplicația apare în share sheet-ul Android
- [ ] Share dintr-o poză din Galerie → ajunge pe PC
- [ ] Share multiplu (mai multe poze deodată)
- [ ] Share dintr-o aplicație random (WhatsApp, Files) → merge

📖 **Ăsta e tot.** Dacă restul fazei e mediocru dar asta merge, ai câștigat. E singurul lucru pe care web UI-ul nu îl poate face — o pagină web nu poate fi destinație de share pe Android.

📖 Gestionarea intent-ului e mai subtilă decât pare: primești un `content://` URI, nu un path. Trebuie citit prin `ContentResolver`. Nu ai voie să presupui că e un fișier pe disc.

---

## Task 8 — Distribuție

- [ ] Publish Release pentru `net10.0-android` (Build → Publish, sau `dotnet publish -f net10.0-android -c Release`)
- [ ] APK semnat cu un keystore pe care **îl păstrezi** (îl pierzi = nu mai poți face update peste instalarea existentă, niciodată)
- [ ] Sideload pe Samsung

📖 Fără Play Store. Nu îți trebuie pentru uz personal, și publicarea ar aduce un review process pentru o aplicație care vorbește doar cu PC-ul tău.

---

## Dacă MAUI te enervează prea tare

E scenariul realist, MAUI e veriga slabă a stack-ului C# ([[00-arhitectura#6. De ce C#]]). Escape hatch: **Kotlin + Android Studio**.

Tooling incomparabil mai bun, documentație mai bună, share sheet mai simplu de făcut. Costul: al doilea limbaj.

Dar observă ceva: **serverul tău nu se schimbă cu nimic.** E HTTP. Un client Kotlin lovește aceleași endpoint-uri. Faptul că poți schimba complet clientul fără să atingi serverul e chiar dovada că arhitectura de la [[00-arhitectura]] e corectă — și e un motiv în plus pentru care nu ai inventat un protocol propriu peste socket brut.

---

## ✅ Definition of done

- Share dintr-o poză din Galerie → ajunge pe PC, fără să deschizi browserul
- Pairing prin QR merge, tokenul e în SecureStorage
- Fișiere mari nu omoară aplicația
- Web UI-ul **încă merge** (nu îl arunci — e fallback-ul pentru iPhone și laptopuri)

→ [[06-backlog]]
