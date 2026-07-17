# Faza 2 — PC → Telefon

**Obiectiv:** deschizi pagina pe telefon, vezi ce fișiere sunt pe PC, apeși pe unul, se descarcă.

**Prerequisite:** [[01-faza-1-telefon-catre-pc]] complet, testat pe telefonul real.

Faza asta e semnificativ mai ușoară decât prima. Partea grea (rețea, firewall, streaming) e deja rezolvată — acum doar folosești canalul în cealaltă direcție.

---

## Task 1 — Endpoint de listare

```csharp
app.MapGet("/api/files", () =>
    new DirectoryInfo(storageFolder)
        .GetFiles()
        .Select(f => new FileItemDto(
            Id: /* vezi Task 2 */,
            Name: f.Name,
            SizeBytes: f.Length,
            ModifiedUtc: f.LastWriteTimeUtc
        )));
```

- [ ] `GET /api/files` întoarce JSON cu lista
- [ ] Testezi din browser pe PC întâi, apoi de pe telefon

📖 **De ce `SizeBytes` și nu `"4.2 MB"`:** formatarea e treaba UI-ului, nu a API-ului. Serverul trimite date, clientul le prezintă. Dacă trimiți string formatat, JS-ul nu mai poate sorta după mărime.

📖 **De ce `ModifiedUtc` și nu ora locală:** dacă telefonul și PC-ul sunt în fusuri diferite (sau doar la DST), orele locale mint. Regula: **UTC pe sârmă, local doar la afișare.**

---

## Task 2 — Cum identifici un fișier în URL

Tentația e `GET /api/files/download/poza.jpg`. **Nu face asta.**

Aceeași problemă de path traversal ca la [[01-faza-1-telefon-catre-pc#Task 8]], dar în direcția inversă și mai gravă: `GET /api/files/download/..%2F..%2F..%2FWindows%2FSystem32%2Fconfig%2FSAM` — și tocmai ai trimis registry-ul de Windows pe internet.

Două opțiuni:

**A. Sanitizare** — accepți filename în URL, dar îl treci prin `Path.GetFileName` și verifici că path-ul rezolvat e sub `storageFolder`. Simplu, dar te bazezi pe validare la fiecare endpoint și e ușor să uiți.

**B. ID opac** — ții un dicționar `Guid → path` în memorie, populat la listare. URL-ul devine `/api/files/download/a3f2-...`. Clientul nu poate cere decât ce i-ai listat tu. Path traversal devine imposibil *prin construcție*, nu prin vigilență.

**Recomandare: B.** E mai puțin cod decât pare, și e o lecție bună — cea mai bună validare e cea de care nu mai ai nevoie.

- [ ] Alegi o abordare (B) și o implementezi
- [ ] Dicționarul se repopulează la fiecare listare (fișierele se pot șterge din Explorer între timp)

---

## Task 3 — Download cu streaming

```csharp
app.MapGet("/api/files/{id}/download", (Guid id) => {
    if (!map.TryGetValue(id, out var path)) return Results.NotFound();
    return Results.File(path, "application/octet-stream", Path.GetFileName(path));
});
```

`Results.File` face streaming automat — nu îți încarcă fișierul în RAM. Aici .NET îți face treaba de la Task 7 din Faza 1 gratis.

- [ ] Descarci un fișier de pe PC pe telefon
- [ ] Testezi cu unul mare — verifici că RAM-ul serverului rămâne plat

📖 **`Content-Disposition`:** al treilea parametru pune header-ul `Content-Disposition: attachment; filename="..."`. Ăsta îi spune browserului *"descarcă asta, nu încerca să o afișezi"*. Fără el, Chrome deschide poza în tab în loc să o salveze. Cu `inline` în loc de `attachment` obții comportamentul opus. E fix genul de metadata pe care ar fi trebuit să ți-o inventezi singur peste socket brut.

📖 **`application/octet-stream`** = "bytes, nu întreb ce sunt". Dacă vrei ca pozele să se deschidă în preview, detectează MIME type-ul real după extensie (`FileExtensionContentTypeProvider` din ASP.NET Core).

---

## Task 4 — Lista în UI

- [ ] `fetch("/api/files")` → randezi lista în HTML
- [ ] Fiecare item e un `<a href="/api/files/{id}/download">` — nu îți trebuie JS pentru download, browserul știe
- [ ] Afișezi mărimea formatată (KB/MB/GB) și data
- [ ] Iconiță/emoji după extensie (🎵 mp3, 📄 pdf, 🖼️ jpg, 🎬 mp4)
- [ ] Buton de refresh

📖 **De ce `<a href>` și nu `fetch` + Blob:** cu un link simplu, browserul gestionează singur descărcarea — bară de progres nativă, download manager, resume, salvare în Downloads. Cu `fetch` + Blob ai încărca tot fișierul în RAM-ul telefonului înainte să îl salvezi, ceea ce omoară tab-ul la un fișier de 4 GB. **Uneori soluția simplă e și cea corectă tehnic.**

---

## Task 5 — Unifică cele două direcții

Ai acum două fluxuri separate. Fă din ele o singură pagină coerentă:

- [ ] O secțiune "Trimite pe PC" (upload din Faza 1)
- [ ] O secțiune "Fișiere pe PC" (lista + download)
- [ ] Lista se reîmprospătează automat după un upload reușit
- [ ] Drag & drop pe zona de upload (funcționează pe desktop, bonus gratuit — `ondrop` + `dataTransfer.files`)

---

## Task 6 — Ștergere (opțional)

```csharp
app.MapDelete("/api/files/{id}", ...);
```

- [ ] Endpoint de delete
- [ ] Confirmare în UI înainte de ștergere
- [ ] Mută în Recycle Bin, nu `File.Delete` (vezi mai jos)

📖 **Recycle Bin, nu delete permanent.** `File.Delete` e ireversibil. Pentru o aplicație în care apeși pe un ecran mic cu degetul gros, un tap greșit nu ar trebui să distrugă date. Se face cu `FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)` din `Microsoft.VisualBasic.FileIO` — da, namespace-ul e ciudat, dar e parte din .NET și e API-ul corect pe Windows.

---

## ✅ Definition of done

- Vezi lista de fișiere de pe PC, pe telefon
- Descarci un fișier apăsând pe el
- Ambele direcții în aceeași pagină
- Nu poți cere un fișier din afara folderului configurat

**În punctul ăsta ai o unealtă completă și utilă.** Tot ce urmează e polish, securitate și confort — nu funcționalitate nouă.

→ [[03-faza-3-securitate-si-pairing]]
