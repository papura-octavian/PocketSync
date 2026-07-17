# Faza 3 — Securitate și pairing

**Obiectiv:** nu mai poate oricine de pe WiFi să îți listeze și descarce fișierele.

**Prerequisite:** [[02-faza-2-pc-catre-telefon]] complet.

---

## Realizează întâi problema

Deschide de pe alt device din casă (laptop, tabletă, telefonul altcuiva) `http://192.168.1.42:5000`.

**Merge.** Fără nimic. Toate fișierele tale, listate frumos, descărcabile.

Serverul tău ascultă pe `0.0.0.0` — adică pe toată rețeaua. Orice e pe WiFi-ul tău vede asta: vizitatori, un bec smart compromis, vecinul care are parola de acum trei ani. Faza asta rezolvă exact atât.

Recitește [[00-arhitectura#7. Threat model — de la cine te aperi]] înainte să începi, ca să știi unde e granița.

---

## Task 1 — Generare token

```csharp
var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
```

- [ ] Token generat la prima pornire, dacă nu există deja unul salvat
- [ ] Se folosește `RandomNumberGenerator`, **nu** `Random` și **nu** `Guid.NewGuid()`

📖 **De ce nu `Random`:** `Random` e un PRNG deterministic, seed-uit din ceas. E predictibil. E făcut pentru simulări și jocuri, nu pentru secrete. `RandomNumberGenerator` cere entropie de la sistemul de operare (CryptGenRandom pe Windows).

📖 **De ce nu `Guid.NewGuid()`:** un GUID v4 are 122 de biți de entropie, ceea ce ar fi *suficient*, dar specificația nu garantează că sunt criptografic random — doar unici. Unicitate ≠ impredictibilitate. Sunt lucruri diferite și e o confuzie clasică. Când vrei un secret, cere explicit un secret.

---

## Task 2 — Salvare securizată cu DPAPI

Tokenul nu are ce căuta în plaintext într-un fișier de config.

```csharp
var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
```

- [ ] Token salvat criptat în `%APPDATA%\PocketSync\`
- [ ] Se încarcă la pornire; dacă lipsește, se generează unul nou

📖 **Ce e DPAPI:** un serviciu de Windows care criptează date cu o cheie derivată din contul tău de utilizator. Cu `CurrentUser`, doar userul tău, pe mașina asta, poate decripta. Alt user de pe același PC nu poate. Copiat pe alt PC, fișierul e inutil. Nu ții tu nicio cheie — de aia e frumos.

📖 **De ce nu în `appsettings.json`:** ajunge în git, în backup, în screenshot când ceri ajutor pe Discord. Secretele stau în `%APPDATA%`, config-ul în `appsettings.json`, și granița e clară: *config = ce ai schimba; secret = ce ai regenera.*

---

## Task 3 — Middleware de autentificare

```csharp
app.Use(async (ctx, next) => {
    if (ctx.Request.Path.StartsWithSegments("/api/status")) { await next(); return; }
    if (!IsAuthenticated(ctx)) { ctx.Response.StatusCode = 401; return; }
    await next();
});
```

- [ ] Middleware care verifică tokenul
- [ ] `/api/status` rămâne public (diagnostic — vrei să știi că serverul trăiește fără să te autentifici)
- [ ] Restul endpoint-urilor sunt blocate
- [ ] Verifici de pe alt device că acum primești 401

📖 **Compară tokenul în timp constant.** `a == b` pe stringuri iese la primul caracter diferit, deci durata comparației dezvăluie *câte caractere ai ghicit corect*. Cu suficiente încercări, se poate reconstrui tokenul caracter cu caracter. Folosește `CryptographicOperations.FixedTimeEquals`. Pe LAN e un atac teoretic, dar e reflexul corect și te costă o linie.

📖 **Ce e un middleware:** o funcție care vede fiecare request înainte să ajungă la endpoint și decide dacă îl lasă mai departe (`next()`) sau îl oprește. Ordinea în care le adaugi *este* ordinea în care rulează — e un pipeline. Pune-l înainte de `UseStaticFiles()`, altfel pagina HTML se servește fără auth.

---

## Task 4 — Cum trimite clientul tokenul

Trei mecanisme, și fiecare are un rol:

| Unde | Pentru ce | Problema |
|---|---|---|
| Query string `?token=x` | doar la primul acces (din QR) | rămâne în istoric, în logs, în Referer |
| Header `X-Auth-Token` | apeluri din JS și din aplicația MAUI | browserul nu îl trimite la `<a href>` |
| Cookie | tot ce face browserul | nu merge din aplicații non-browser |

**Fluxul corect:**

1. QR-ul conține `http://192.168.1.42:5000/?token=abc`
2. Serverul vede tokenul în query, îl validează, setează un **cookie** `HttpOnly` + `SameSite=Strict`
3. Serverul face **redirect la `/`** — fără token în URL
4. De atunci încolo, browserul trimite cookie-ul automat la fiecare request, inclusiv la `<a href>` de download

- [ ] Accepți token din query string, o singură dată, la intrare
- [ ] Setezi cookie și faci redirect ca să scoți tokenul din URL
- [ ] Accepți și header `X-Auth-Token` (pentru Faza 5 și pentru curl)
- [ ] Cookie-ul e `HttpOnly` (JS nu îl poate citi → XSS nu îl poate fura)

📖 **De ce redirect-ul contează:** dacă lași `?token=abc` în bara de adrese, tokenul ajunge în istoricul Chrome, în sincronizarea cu contul Google, și în header-ul `Referer` dacă pagina linkează vreodată în afară. Redirect-ul îl mută în cookie și șterge urma. Costă trei linii.

📖 **De ce `<a href>` forțează cookie-ul:** la Task 4 din [[02-faza-2-pc-catre-telefon]] ai folosit link simplu pentru download, ca browserul să gestioneze fișierele mari. Dar un `<a href>` nu poate purta header custom — JS-ul nu e implicat. Deci ai nevoie de cookie. Decizia de UI din faza trecută determină mecanismul de auth din faza asta; așa arată constrângerile reale.

---

## Task 5 — QR code

NuGet: `QRCoder`

Conținutul QR-ului: `http://192.168.1.42:5000/?token=abc123`

- [ ] Generezi QR-ul la pornire, îl afișezi (deocamdată console app → salvezi PNG și îl deschizi, sau îl scrii ca ASCII în consolă cu `AsciiQRCode`)
- [ ] IP-ul se detectează automat, nu îl scrii de mână
- [ ] Afișezi și URL-ul ca text (pentru când scanarea nu merge)
- [ ] Scanezi cu **aplicația de cameră a telefonului** → se deschide Chrome → ești deja înăuntru

📖 **Ăsta e câștigul din [[00-arhitectura#5. De ce browserul, și nu aplicație mobilă, în Faza 1]]:** pairing complet funcțional, fără nicio linie de cod mobil. Camera Samsung știe deja să scaneze QR-uri și să deschidă URL-uri.

**Detectarea IP-ului e mai subtilă decât pare.** PC-ul are mai multe adrese: loopback, WiFi, Ethernet, plus adaptoare virtuale de la VirtualBox/WSL/VPN/Hyper-V. Dacă alegi greșit, QR-ul conține un IP la care telefonul nu poate ajunge, și nu ai niciun indiciu de ce. Filtrează cu `NetworkInterface.GetAllNetworkInterfaces()` după `OperationalStatus.Up`, exclude loopback și tunele. Dacă rămân mai multe, arată-le pe toate și lasă utilizatorul să aleagă — mai bine o întrebare decât un QR mort.

---

## Task 6 — Regenerare token

- [ ] Comandă/buton care generează token nou și invalidează cookie-urile vechi
- [ ] După regenerare, QR-ul afișat se actualizează

📖 De ce: dacă ai dat vreodată telefonul cuiva, sau ai scanat QR-ul pe un device pe care nu îl mai controlezi, ăsta e butonul de panică. Orice sistem cu credențiale are nevoie de o cale de revocare — altfel un secret compromis rămâne compromis pe veci.

---

## ⚠️ Ce NU faci în faza asta: HTTPS

Tentant, dar e o capcană. Certificat self-signed → Chrome pe Android urlă cu warning roșu, iar din Android 7 aplicațiile nu mai acceptă CA-uri instalate de user fără config special. Vei pierde zile pe trust store în loc să transferi fișiere.

Rezultatul: pe LAN-ul tău, tokenul circulă în clar. Cineva care deja *ascultă* traficul din rețeaua ta îl poate lua. Pentru uz personal acasă, e un compromis conștient și acceptabil.

Devine inacceptabil dacă: dai aplicația altcuiva, sau o expui în afara casei. Ambele sunt în [[06-backlog#HTTPS ca lumea]], unde e și soluția corectă.

**Notează asta undeva.** E o datorie tehnică asumată, nu una uitată. Diferența dintre cele două e că prima e scrisă.

---

## ✅ Definition of done

- De pe un device nepairat: 401 la tot
- Scanezi QR-ul cu camera → Chrome se deschide → merge, fără să tastezi nimic
- Tokenul nu apare în bara de adrese după primul acces
- Tokenul e criptat pe disc
- Știi exact ce nu protejează setup-ul ăsta și ai notat-o

→ [[04-faza-4-tray-app]]
