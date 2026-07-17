# Faza 4 — Tray app (WPF)

**Obiectiv:** nu mai pornești un console app din terminal. E o aplicație reală, cu icon lângă ceas, care pornește cu Windows.

**Prerequisite:** [[03-faza-3-securitate-si-pairing]] complet.

Faza asta nu adaugă **nicio** funcționalitate de transfer. Adaugă doar ambalajul. E important să știi asta — dacă te blochezi aici, unealta ta continuă să funcționeze.

---

## Task 1 — Separă serverul de host

Momentan ai un singur proiect care e și server, și executabil. Împarte-l:

```
PocketSync.sln
├── PocketSync.Server/     (class library — endpoint-uri, servicii, wwwroot)
└── PocketSync.TrayApp/    (WPF — referință la Server)
```

- [ ] `PocketSync.Server` devine class library, expune un `StartAsync(config)` / `StopAsync()`
- [ ] Console app-ul vechi îl poți păstra ca `PocketSync.Cli` pentru debugging rapid — e mult mai plăcut să testezi API-ul fără să pornești WPF

📖 **De ce separi:** `WebApplication` poate rula în orice proces .NET, nu doar într-un `dotnet new web`. Nu e legat de tipul de proiect. Separarea îți permite să pornești serverul din CLI când debughezi rețeaua, și din WPF când testezi UI-ul — două scenarii, un singur cod.

---

## Task 2 — WPF gol cu tray icon

NuGet: `Hardcodet.NotifyIcon.Wpf`

WPF nu are suport nativ pentru tray icon. Librăria asta e standardul de facto (wrapper peste API-ul Win32 `Shell_NotifyIcon`).

- [ ] Proiect WPF, fereastra principală ascunsă la pornire
- [ ] Icon în tray, meniu context cu un singur item: **Ieșire**
- [ ] Închiderea din X **ascunde** fereastra, nu omoară aplicația (`e.Cancel = true` în `OnClosing` + `Hide()`)
- [ ] Ieșire din meniu chiar închide aplicația

📖 **Capcana clasică:** dacă `ShutdownMode` rămâne default (`OnLastWindowClose`), aplicația moare când ascunzi ultima fereastră — și rămâi cu un icon fantomă în tray. Setează `ShutdownMode="OnExplicitShutdown"` în `App.xaml`.

---

## Task 3 — Pornește serverul din WPF

În `App.xaml.cs → OnStartup`:

```csharp
_server = new PocketSyncServer(config);
_ = _server.StartAsync();   // NU await — nu bloca thread-ul de UI
```

- [ ] Serverul pornește la lansarea aplicației
- [ ] UI-ul rămâne responsive (dacă îngheață, ai făcut `await` sau `.Wait()` pe UI thread)
- [ ] La Ieșire: `await _server.StopAsync()` **înainte** de `Shutdown()`
- [ ] Verifici din telefon că merge exact ca înainte

📖 **Un singur `.exe`, un singur proces.** Nu pornești serverul ca proces separat. Kestrel rulează pe thread-urile lui din thread pool, WPF pe UI thread-ul lui, în același proces. Un singur instalator, o singură icoană în Task Manager, un singur lucru de închis.

📖 **De ce nu `await` în `OnStartup`:** `app.RunAsync()` nu se termină niciodată — rulează cât trăiește serverul. Dacă faci `await`, `OnStartup` nu se termină niciodată și UI-ul nu apare. `_ = ` spune explicit "știu că e fire-and-forget". Dar atenție: excepțiile dintr-un task neurmărit dispar în tăcere — atașează un `.ContinueWith` care loghează, altfel serverul poate muri fără ca tu să afli vreodată.

---

## Task 4 — Fereastra de status

Minimă:

- [ ] Status: 🟢 "Activ pe 192.168.1.42:5000" / 🔴 "Oprit"
- [ ] QR code (același din [[03-faza-3-securitate-si-pairing#Task 5]], acum într-un `<Image>` real)
- [ ] URL-ul ca text selectabil
- [ ] Buton "Deschide folderul" → `Process.Start("explorer.exe", storageFolder)`
- [ ] Ultimele fișiere transferate

📖 Aici QR-ul își găsește în sfârșit casa. Până acum era un PNG salvat pe disc sau ASCII în consolă — hack de dezvoltare. Acum e o fereastră pe care o deschizi când vine un prieten cu telefonul.

---

## Task 5 — Setări

- [ ] Folder de storage (cu `FolderBrowserDialog`)
- [ ] Port
- [ ] Buton "Regenerează token" (cu confirmare — invalidează pairing-urile existente)
- [ ] Toggle "Pornește cu Windows"

📖 **Schimbarea portului cere restart la server** (`StopAsync` + `StartAsync` cu config nou) **și o regulă nouă de firewall.** Ori gestionezi asta explicit, ori — mai bine pentru MVP — faci portul read-only în UI și îl schimbi doar din `appsettings.json`. Un setting care pare că merge dar lasă firewall-ul în urmă e mai rău decât un setting care lipsește.

---

## Task 6 — Pornire cu Windows

Registry key:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

- [ ] Toggle în Setări scrie/șterge cheia
- [ ] **Default: OFF.** Utilizatorul activează conștient.
- [ ] Aplicația pornește minimizată în tray, nu cu fereastra deschisă

📖 **De ce `HKCU` și nu `HKLM`:** `HKCU` e per-user și nu cere drepturi de administrator. `HKLM` cere UAC și pornește aplicația pentru toți userii de pe PC — nu e ce vrei.

📖 **De ce default OFF:** o aplicație care se auto-instalează la startup fără să întrebe e comportament de malware. Pe lângă că e nepoliticos, unele antivirusuri chiar flag-uiesc scrierea în `Run` ca suspectă. Un toggle vizibil, oprit by default, e și corect și te scutește de false positive.

---

## Task 7 — Notificări toast

- [ ] Toast la fișier primit: "poza.jpg (2.4 MB) primită de la telefon"
- [ ] Click pe toast → deschide folderul

📖 Ăsta e feedback-ul care lipsește. Când trimiți de pe telefon, telefonul îți zice că a mers — dar PC-ul tace. Toast-ul închide bucla.

---

## Task 8 — Logging

NuGet: `Serilog` + `Serilog.Sinks.File`

- [ ] Log în `%APPDATA%\PocketSync\logs\`
- [ ] Rolling pe zi, păstrezi ~7 zile
- [ ] Loghezi: pornire/oprire server, fiecare transfer, fiecare 401, fiecare excepție
- [ ] **NU** loghezi tokenul

📖 De ce acum și nu mai devreme: în console app aveai output-ul în față. Într-o aplicație de tray, când ceva nu merge la 2 noaptea, log-ul e singura ta fereastră. Un tray app fără log e o cutie neagră.

---

## Task 9 — Instalator (opțional)

**Inno Setup** — gratuit, standard pentru aplicații desktop Windows.

- [ ] `dotnet publish -c Release -r win-x64 --self-contained`
- [ ] Script Inno Setup
- [ ] Instalatorul adaugă regula de firewall (are drepturi de admin, deci poate) — asta scutește utilizatorul de PowerShell
- [ ] Uninstall curat

📖 **Instalatorul e locul potrivit pentru regula de firewall.** Aplicația în sine rulează fără admin (și așa trebuie). Instalatorul rulează *o dată*, cu admin. Ăsta e exact tipul de operație care aparține acolo: privilegiat, o singură dată, la instalare.

---

## ✅ Definition of done

- Icon în tray, aplicația pornește cu Windows (dacă vrei tu)
- QR-ul e la un click distanță
- Toast la fiecare fișier primit
- Log pentru când ceva nu merge
- Funcționalitatea de transfer e **exact aceeași** — ai schimbat doar ambalajul

→ [[05-faza-5-app-android]]
