# Faza 1 — Telefon → PC

**Obiectiv:** apeși un buton în Chrome pe Samsung, alegi un fișier, fișierul apare într-un folder pe PC.

**La final ai:** o unealtă pe care chiar o folosești. Nu un backend pe jumătate.

**Ce NU e în faza asta:** autentificare, QR, tray icon, listare fișiere, download, aplicație mobilă. Toate vin după. Rezistă tentației.

---

## Task 1 — Proiect gol care pornește

```
dotnet new web -n PocketSync.Server
cd PocketSync.Server
dotnet run
```

`dotnet new web` = template Minimal API. Nu `webapi` (aduce controllere și Swagger de care nu ai nevoie), nu `mvc` (aduce Razor).

- [ ] Proiectul rulează, vezi în consolă pe ce port ascultă
- [ ] Deschizi `http://localhost:5000` în browserul de pe PC și vezi ceva

📖 **Ce s-a întâmplat:** `WebApplication.CreateBuilder()` a configurat Kestrel, `app.Run()` a deschis un socket de ascultare pe portul 5000 și a intrat într-o buclă infinită de accept. Codul de socket din [[00-arhitectura#1. Stratul de dedesubt socket-uri]] există — doar că l-a scris altcineva.

---

## Task 2 — Endpoint `/api/status`

Cel mai simplu endpoint posibil, dar e unealta ta de diagnostic pentru tot restul proiectului. Când ceva nu merge, ăsta îți spune dacă problema e în rețea sau în cod.

```csharp
app.MapGet("/api/status", () => new { ok = true, version = "0.1" });
```

- [ ] `http://localhost:5000/api/status` întoarce JSON în browser

📖 **De ce JSON și nu text:** ca să nu îl schimbi mai târziu când vrei să adaugi câmpuri (nume PC, spațiu liber pe disc). Minimal API serializează automat obiectul anonim.

---

## Task 3 — Ascultă pe toată rețeaua, nu doar pe localhost

**Ăsta e primul lucru care te blochează.** Default-ul e `localhost`, adică `127.0.0.1` — o interfață de rețea virtuală care există doar în interiorul PC-ului. Telefonul nu are cum să ajungă acolo. Niciodată. Oricât ai încerca.

Trebuie să asculți pe `0.0.0.0`, ceea ce înseamnă "toate interfețele de rețea ale acestui PC", inclusiv placa de WiFi.

În `Properties/launchSettings.json`, sau direct:

```csharp
app.Run("http://0.0.0.0:5000");
```

Apoi află IP-ul tău din LAN:

```
ipconfig
```

Cauți `IPv4 Address` de la adaptorul de WiFi. Ceva de tipul `192.168.1.42` sau `192.168.0.x`.

- [ ] Serverul pornește și în consolă scrie `0.0.0.0:5000`, nu `localhost:5000`
- [ ] Ai notat IP-ul PC-ului: `192.168.___.___`

📖 **De ce contează `0.0.0.0` vs `127.0.0.1`:** un socket de ascultare e legat de o adresă IP specifică. `127.0.0.1` = "accept doar conexiuni care vin din interiorul meu". `0.0.0.0` = wildcard, "accept pe orice adresă am". PC-ul tău are mai multe adrese simultan: `127.0.0.1` (loopback), `192.168.1.42` (WiFi), poate încă una de la Ethernet sau de la VirtualBox.

---

## Task 4 — Firewall (momentul adevărului)

Încearcă acum, de pe telefon, în Chrome: `http://192.168.1.42:5000/api/status`

**Va da timeout.** Codul tău e corect. Windows Firewall blochează conexiunile inbound către procese care nu au regulă.

Rulează în PowerShell **ca administrator**:

```powershell
New-NetFirewallRule -DisplayName "PocketSync" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private
```

`-Profile Private` = regula se aplică doar pe rețele marcate ca "Private" (rețeaua de acasă), nu pe "Public" (WiFi-ul de la cafenea). Verifică în Settings → Network că rețeaua ta de acasă e setată **Private**, altfel regula nu prinde.

- [ ] Ai adăugat regula de firewall
- [ ] **De pe telefon, în Chrome, vezi JSON-ul de la `/api/status`** ← Ăsta e momentul cel mai important din tot proiectul

Dacă tot nu merge, în ordinea asta:
1. Telefonul e pe același WiFi? (nu pe date mobile, nu pe banda de guest)
2. Rețeaua e Private, nu Public?
3. Router-ul are "AP isolation" / "client isolation" activat? (unele routere blochează device-to-device by default)
4. Serverul chiar ascultă pe `0.0.0.0`? Verifică cu `netstat -an | findstr 5000`

📖 **Ce ai demonstrat:** ai un canal TCP funcțional între două device-ei fizice. Tot restul proiectului e doar *ce trimiți prin el*. Partea grea de rețea s-a terminat aici.

---

## Task 5 — O pagină HTML servită de server

Creezi `wwwroot/index.html` și în `Program.cs`:

```csharp
app.UseDefaultFiles();   // /  →  /index.html
app.UseStaticFiles();    // servește ce e în wwwroot/
```

HTML-ul, deocamdată minimal:

```html
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PocketSync</title>
</head>
<body>
  <h1>PocketSync</h1>
  <input type="file" id="file">
  <button id="send">Trimite</button>
  <div id="status"></div>
</body>
</html>
```

- [ ] Deschizi `http://192.168.1.42:5000` pe telefon și vezi pagina
- [ ] `<meta viewport>` e acolo (fără el pagina arată microscopic pe telefon)

📖 **Fără framework.** Nu îți trebuie React aici. Sunt trei elemente și 30 de linii de JS. Un framework ar adăuga un build step, `node_modules`, și zero valoare.

---

## Task 6 — Upload, varianta simplă (care se va rupe)

Endpoint:

```csharp
app.MapPost("/api/upload", async (IFormFile file) => {
    var path = Path.Combine(storageFolder, file.FileName);
    using var stream = File.Create(path);
    await file.CopyToAsync(stream);
    return Results.Ok();
}).DisableAntiforgery();
```

JS în pagină:

```js
const fd = new FormData();
fd.append("file", document.getElementById("file").files[0]);
await fetch("/api/upload", { method: "POST", body: fd });
```

- [ ] Trimiți o poză de pe telefon, apare în folderul de pe PC
- [ ] **Ai transferat primul fișier.** Sărbătorește.

📖 **Ce e `multipart/form-data`:** un format care împachetează mai multe câmpuri (nume + conținut binar) într-un singur body HTTP, separate printr-un delimitator random numit *boundary*. Deschide DevTools → Network și uită-te la request-ul brut ca să vezi cum arată. E fix genul de protocol pe care ar fi trebuit să îl inventezi singur peste socket brut.

---

## Task 7 — Rupe-l intenționat, apoi repară-l

Trimite un fișier mare. Un mp4 de 2-4 GB.

Vei vedea una din două: fie eroare `413 Payload Too Large` (limita default e ~28 MB), fie procesul mănâncă RAM proporțional cu fișierul.

**Nu ridica pur și simplu limita.** Aia mută problema, nu o rezolvă.

Problema reală: `IFormFile` înseamnă *"ASP.NET Core a citit deja tot fișierul înainte să îți dea ție controlul"*. Îl ține în RAM (sub un prag) sau într-un temp file. Tu îl copiezi apoi a doua oară la destinație. Deci un fișier de 4 GB e scris de două ori și/sau ținut în memorie.

Soluția: **streaming**. Citești din request și scrii pe disc bucată cu bucată, fără să ții niciodată tot fișierul nicăieri. Cu `MultipartReader`, dezactivând form binding-ul automat.

- [ ] Reproduci problema cu un fișier mare (uită-te în Task Manager la RAM-ul procesului)
- [ ] Rescrii pe `MultipartReader` + `CopyToAsync` direct în `FileStream`
- [ ] Retrimiți fișierul de 4 GB — RAM-ul rămâne plat
- [ ] Ridici și `MaxRequestBodySize` (sau îl setezi `null` = fără limită)

📖 **Lecția, și e una mare:** diferența dintre *"am tot fișierul în mână"* și *"primesc bytes pe măsură ce vin"*. E fix întrebarea din socket brut — bucățile vin oricum una câte una, HTTP doar le ascunsese de tine. Un `Stream` în .NET e exact abstracția asta: nu știi cât mai vine, doar citești până se termină.

---

## Task 8 — Validare (nu e opțional)

Trei lucruri:

**1. Path traversal.** `file.FileName` vine de la client, deci e ostil prin definiție. Cineva poate trimite `..\..\..\Windows\System32\evil.dll`. `Path.Combine` **nu** te apără — dacă al doilea argument e path absolut sau are `..`, rezultatul iese din folder.

```csharp
var safeName = Path.GetFileName(file.FileName);   // aruncă orice cale
var full = Path.GetFullPath(Path.Combine(storageFolder, safeName));
if (!full.StartsWith(storageFolder)) return Results.BadRequest();  // centură + bretele
```

**2. Coliziuni de nume.** Al doilea `IMG_1234.jpg` îl suprascrie pe primul. Ori adaugi sufix (`IMG_1234 (2).jpg`), ori timestamp. Alege una.

**3. Nume invalide pe Windows.** Android permite `:` și `?` în nume, Windows nu. Fișierul va eșua la scriere cu o eroare criptică.

- [ ] `Path.GetFileName` + verificare că path-ul final e sub folderul de storage
- [ ] Testezi cu curl trimițând `../../test.txt` ca filename și confirmi că e respins
- [ ] Gestionezi coliziunile
- [ ] Sanitizezi caracterele interzise pe Windows

📖 **De ce contează chiar și fără auth:** în Faza 3 pui token, dar validarea input-ului nu e înlocuită de autentificare. Un atacator autentificat e tot un atacator. Regula: *niciodată nu construi un path din string venit de la client, fără să îl treci prin `Path.GetFileName` și să verifici rezultatul.*

---

## Task 9 — Progres la upload

`fetch()` **nu** îți dă progres la upload (doar la download). Ori folosești `XMLHttpRequest` (are `upload.onprogress`), ori te lipsești.

Pentru un mp4 de 4 GB pe WiFi, o bară de progres nu e cosmetică — fără ea nu știi dacă merge sau s-a blocat.

- [ ] `XMLHttpRequest` + `upload.onprogress` → `<progress>` în pagină
- [ ] Afișezi și viteza (MB/s) — util ca să vezi dacă WiFi-ul e problema

---

## Task 10 — Configurare

Scoate din cod ce e hardcodat, în `appsettings.json`:

```json
{
  "PocketSync": {
    "StorageFolder": "C:\\Users\\kebul\\PocketSync",
    "Port": 5000,
    "MaxFileSizeMb": 0
  }
}
```

- [ ] Folderul de storage se creează automat dacă nu există
- [ ] Portul se citește din config

---

## ✅ Definition of done

- Trimiți un fișier de pe Samsung pe PC, prin Chrome, prin WiFi
- Merge și cu un mp4 de câțiva GB, fără să mănânce RAM
- Vezi progresul
- Un filename ostil nu scrie în afara folderului

**Nu treci mai departe până toate cinci nu sunt bifate pe telefonul real.**

→ [[02-faza-2-pc-catre-telefon]]
