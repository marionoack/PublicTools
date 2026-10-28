# Umsetzungsplan: Application Starter

Stand: 28.10.2026. Dieser Plan dokumentiert den abgestimmten Umfang
einschliesslich der nachtraeglichen Erweiterungen.

## Rahmenbedingungen

- Windows-Anwendung mit .NET 10 und WPF, fuer einen einzelnen Benutzer.
- Programme unter dem aktuellen Windows-Konto oder gespeicherten lokalen Konten
  starten, ohne Abmeldung oder Anmeldung an einem anderen Windows-Desktop.
- Ohne ausdrueckliche UAC-Option keine zusaetzliche Rechteerhoehung;
  keine Umgehung von UAC oder Richtlinien.
  Windows-Anmelderechte und Kontoberechtigungen gelten weiterhin.
- Deutsche Oberflaeche; normale GUI-Anwendungen oeffnen eigene Fenster.
  Konsole, Dateibrowser und nichtinteraktive Ausfuehrungslogs werden als
  Arbeits-Tabs eingebettet.

## Arbeitspakete und Umsetzungsstand

### 1. Projektbasis und Benutzerkonten

- [x] WPF-Projekt fuer .NET 10 erstellen.
- [x] Das tatsaechlich aktive Windows-Konto einschliesslich Domaene bzw.
  Rechnername als nicht loeschbares Standardprofil uebernehmen.
- [x] Beim aktuellen Konto die bestehende Anmeldung verwenden, ohne gespeicherte
  Zugangsdaten oder erneuten Aufruf einer Windows-Anmeldung.
- [x] Bereits vorhandene lokale Administrator- und Nichtadministratorkonten
  mit Anzeigename, Benutzername und Passwort konfigurierbar machen.
  Die Anwendung legt selbst keine Windows-Konten an.

### 2. Speicherung

- [x] Konfiguration unter `%LOCALAPPDATA%\ApplicationStarter` speichern.
- [x] Passwoerter in separaten lokalen Dateien mit Windows-DPAPI
  (`CurrentUser`) verschluesseln; keine Klartextpasswoerter in Konfiguration
  oder Protokollen ablegen.
- [x] Passwortaenderung und ausdrueckliches Entfernen unterstuetzen.
  Ein leeres Passwortfeld beim Bearbeiten behaelt das gespeicherte Passwort bei.
- [x] Den Speicherordner mit Benutzerberechtigungen absichern.

DPAPI schuetzt gespeicherte Daten, aber nicht vor Schadsoftware unter dem
eigenen Windows-Konto oder einem Administrator mit entsprechender Kontrolle.

### 3. Verknuepfungen

- [x] Name, Typ, EXE-Pfad, Argumente, Arbeitsverzeichnis und Startkonto
  pro Verknuepfung konfigurieren.
- [x] Ein eigenes Icon aus einer ICO- oder EXE-Datei auswaehlbar machen.
  Ohne eigene Auswahl das verfuegbare Ziel-Icon verwenden.
- [x] **Windows-Benutzerprofil laden** an der Verknuepfung speichern,
  nicht am Benutzerkonto. Unterschiedliche Verknuepfungen desselben Kontos
  duerfen unterschiedliche Einstellungen haben.
- [x] Diese Einstellung fuer das aktuelle Windows-Konto deaktivieren:
  Dessen Profil ist bereits geladen. Das Laden des Windows-Profils ist
  unabhaengig vom PowerShell-Startskriptprofil.
- [x] Verknuepfungen duplizieren: neue ID, uebernommene Einstellungen,
  vorgeschlagener Name mit Zusatz `(Kopie)` und Bearbeitung vor dem Speichern.
  Das Original bleibt unveraendert; Abbrechen speichert keine Kopie.
- [x] Duplizieren ueber Schaltflaeche und `Strg+D` in Liste oder Suche anbieten.

### 4. Vorkonfiguration

- [x] PowerShell 7 aus der globalen Installation vorbelegen:
  `%ProgramW6432%\PowerShell\7\pwsh.exe`.
- [x] Claude Desktop vorbelegen:
  `%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`.
- [x] Einen Dateibrowser mit `%USERPROFILE%` als Startordner vorbelegen.
- [x] Umgebungsvariablen in Programm-, Argument- und Startpfaden unter dem
  ausgewaehlten Startkonto aufloesen. Eigene Icon-Pfade werden unter dem
  aktuell angemeldeten Konto gelesen.
- [x] Fehlende Programme, Installationen oder Zugriffsrechte verstaendlich melden.

Eine WindowsApps-Verknuepfung garantiert keine Installation fuer andere Konten.

### 5. Benutzerprozesse und Terminal

- [x] Hilfsprozesse unter der jeweils ausgewaehlten Identitaet starten und ueber
  einen lokal abgesicherten Kommunikationskanal ansprechen.
- [x] Hilfsprozesse unterschiedlicher Konten oder Profileinstellungen trennen.
- [x] Ein vollwertiges interaktives Terminal mit Windows ConPTY, xterm.js
  und eingebettetem WebView2 bereitstellen.
- [x] Terminalressourcen lokal mitliefern, ohne CDN-Aufrufe.
- [x] Mehrere Terminal- und Dateibrowser-Tabs mit fest zugeordnetem Konto erlauben.
- [x] Beim Schliessen eines Terminal-Tabs dessen Prozesse beenden.
  Gestartete GUI-Anwendungen bleiben geoeffnet; zugehoerige Hilfsprozesse bleiben
  bei Bedarf fuer die Lebensdauer des geladenen Profils bestehen.

### 6. Dateibrowser

- [x] Laufwerke und Ordner mit den tatsaechlichen Berechtigungen des Tab-Kontos
  durchsuchen; versteckte Dateien optional anzeigen.
- [x] Dateien unter dem ausgewaehlten Konto mit einer direkt startbaren
  Dateizuordnung oeffnen.
- [x] Dateien und Ordner anlegen, umbenennen, kopieren, verschieben und loeschen.
- [x] Vor Ersetzen bzw. Zusammenfuehren vorhandener Ziele bestaetigen lassen.
- [x] Endgueltiges Loeschen ohne Papierkorb deutlich kennzeichnen und bestaetigen.
- [x] Junctions und symbolische Links beim Kopieren und Verschieben nicht verfolgen.

Kopieren und Verschieben sind nicht atomar; nach Fehlern koennen bereits kopierte
Dateien im Ziel verbleiben. Reine Store-/DDE-Dateizuordnungen werden nicht an
die Shell des aktuell angemeldeten Kontos delegiert.

### 7. Kompakte Verknuepfungsliste und Schnellstart

- [x] Beim Start die Schnellsuche fokussieren; auch direktes Tippen in der Liste
  startet die Namensfilterung.
- [x] Namen ohne Beachtung der Gross-/Kleinschreibung filtern.
- [x] Die ersten neun sichtbaren Treffer mit `[1]` bis `[9]` kennzeichnen.
- [x] Mit `Strg+1` bis `Strg+9` den entsprechenden sichtbaren Treffer starten,
  einschliesslich Ziffernblock und Eingabe aus dem Terminal.
- [x] Nach jeder Filteraenderung die Nummerierung neu vergeben.
- [x] Pro Eintrag genau zwei Textzeilen anzeigen: Verknuepfungsname mit
  Schnellstartnummer und Benutzerkonto. Lange Texte kuerzen statt umbrechen.

### 8. Tastaturbedienung und Titelleiste

- [x] Eigenes Anwendungsicon und beim Build erzeugtes Builddatum in die
  Titelleiste aufnehmen.
- [x] Fokus und Auswahl nach Dateibrowseraktionen erhalten bzw. wiederherstellen.
- [x] Die Dateiliste als einzelnen Tabstopp behandeln; Werkzeugleistenaktionen
  ueber eine nachvollziehbare Tab-Reihenfolge erreichbar machen.
- [x] Im Dateibrowser Pfeiltasten, Enter zum Oeffnen, Ruecktaste bzw.
  `Alt+Pfeil oben`, `F2`, `Entf`, `F5`, `Strg+L` bzw. `Alt+D`
  und `Strg+Umschalt+N` unterstuetzen.
- [x] Mit `Strg+Tab` bzw. `Strg+Umschalt+Tab` zwischen Arbeits-Tabs wechseln,
  auch aus der Terminaleingabe.

## Pruefung und verbleibende Punkte

### Direkte PowerShell-Skripte

- [x] Import und Export oben rechts neben "Benutzerkonten" platzieren.
- [x] Neue Typen "PowerShell 5" (Windows PowerShell 5.1) und "PowerShell 7"
  mit mehrzeiligem Editor fuer direkt eingefuegten Skripttext anbieten.
- [x] Den passenden globalen Interpreter automatisch bestimmen; fuer diese
  Typen sind kein EXE-Pfad und keine separaten Argumente erforderlich.
- [x] Pro Verknuepfung zwischen nichtinteraktivem Log-Tab und interaktivem
  Terminal waehlen lassen. Im Terminal bleibt die Shell nach dem Skript offen.
- [x] Konto, Arbeitsverzeichnis, Windows-Profil-Laden und Icon weiter
  unterstuetzen; Skripttext und Ausfuehrungsmodus speichern, duplizieren
  und per JSON uebertragen.
- [x] Skripttext unveraendert an PowerShell uebergeben, insbesondere ohne
  Windows-Umgebungsvariablen oder Anfuehrungszeichen im Skript vorab zu ersetzen.
- [x] Beide PowerShell-Versionen, Log-/Terminalmodus, Dialog und JSON-Rundlauf
  gezielt pruefen und die Release-Ausgabe aktualisieren.

Die Skripte werden ohne temporaere Skriptdateien uebergeben; es gilt die
Windows-Laengenbegrenzung fuer Prozesskommandozeilen inklusive Base64-Kodierung.
PowerShell-Startskriptprofile werden nicht geladen und Ausfuehrungsrichtlinien
nicht geaendert. Log-Ausgaben verwenden UTF-8, auch unter Windows PowerShell 5.1;
Fehler erscheinen als lesbarer Text statt CLIXML. `using namespace` und
anfaengliche `param`-Bloecke bleiben nutzbar.

### UAC fuer PowerShell-Skripte

- [x] Fuer PowerShell 5 und 7 die Option "Mit UAC" pro Verknuepfung anbieten;
  standardmaessig ausgeschaltet, einschliesslich Speicherung, Duplikat und JSON.
- [x] Log- und Terminalmodus weiterhin als Tabs betreiben. Nur den zugeordneten
  Hilfsprozess nach regulaerer Windows-UAC-Bestaetigung erhoehen, nicht die
  gesamte Anwendung.
- [x] Das ausgewaehlte Windows-Konto zwingend beibehalten; Nichtadministratoren
  mit einer verstaendlichen Meldung ablehnen. Einen Kontowechsel im UAC-Dialog
  niemals zur Ausfuehrung des Skripts verwenden.
- [x] Erhoehung und Identitaet vor der Skriptausfuehrung pruefen;
  UAC-Abbruch ohne unberechtigten Fallback behandeln und Tabs kennzeichnen.
- [x] Standardstarts, Kontopruefung, Dialog und JSON pruefen; interaktive
  UAC-Pruefung gesondert aktivierbar machen und Release-Ausgabe aktualisieren.

Die automatischen Pruefungen einschliesslich regulaerem Start eines gespeicherten
Kontos, Nichtadministrator-Ablehnung und SID-Abgleich sind abgeschlossen.
Der echte erhoehte Start mit Windows-UAC-Bestaetigung wurde auf ausdruecklichen
Benutzerwunsch nicht getestet.

### Korrektur der Administratorkonto-Erkennung

- [x] Administrator-Mitgliedschaft auch im UAC-gefilterten Token erkennen;
  ein nicht erhoehter Prozess bedeutet nicht, dass sein Konto kein Administrator ist.
- [x] Die tatsaechliche Erhoehung weiterhin getrennt pruefen; Application Starter
  muss selbst nicht als Administrator laufen.
- [x] Das gemeldete gespeicherte Administratorkonto ohne UAC-Aufruf mit und ohne
  Windows-Profil-Laden pruefen und die Release-Ausgabe aktualisieren.

`WindowsIdentity.Groups` laesst als "Deny-only" markierte Gruppen weg. Die
Kontopruefung beruecksichtigt deshalb zusaetzlich die zugehoerigen SID-Claims.
Der Fehler wurde mit dem gemeldeten Konto vor der Korrektur reproduziert und
danach fuer beide Profilmodi behoben. Die Erhoehungspruefung und die Ablehnung
eines anderen Kontos bleiben unveraendert. Ein echter UAC-Dialog wurde weiterhin
nicht automatisch ausgeloest.

### Wiederholte UAC-Meldung beim Debug-Start

- [x] Tatsaechlichen Startpfad mit dem Benutzer klaeren: verwendet wird
  `bin\Debug\net10.0-windows\ApplicationStarter.exe`.
- [x] Veraltete Debug-Ausgabe feststellen: Stand 15:41 Uhr ohne
  Deny-only-Korrektur; die Release-Ausgabe enthielt die Korrektur bereits.
- [x] Die zuvor durch eine laufende Instanz gesperrte Debug-Ausgabe neu bauen
  und die Kontoerkennung dort mit dem gespeicherten Administratorkonto pruefen.

Die verwendete Debug-Ausgabe wurde um 17:39 Uhr erfolgreich aktualisiert.
Die gespeicherte Administratoridentitaet wird mit und ohne Profil-Laden
korrekt erkannt; getestete Assembly und tatsaechliche Debug-Ausgabe sind
inhaltlich identisch. Es wurde kein echter UAC-Dialog ausgeloest.

### Erweiterung vom 28.10.2026

- [x] Verknuepfungen ueber Pfeil-hoch/-runter-Schaltflaechen umordnen und die
  Reihenfolge speichern. Bei aktivem Filter mit dem benachbarten sichtbaren
  Treffer tauschen; Schnellstartnummern entsprechend neu vergeben.
- [x] Neu, Bearbeiten, Duplizieren und Loeschen als kompakte Symbolschaltflaechen
  mit Tooltips und zugaenglichen Namen darstellen.
- [x] `Strg+E` zum Bearbeiten und `Strg+I` zum Anlegen einer Verknuepfung
  in Liste/Suche anbieten; bestehende Schnellstarts und Terminaleingaben erhalten.
- [x] JSON-Export mit Checkliste zur Auswahl der Verknuepfungen anbieten.
  Kontozuordnungen und Einstellungen exportieren, niemals gespeicherte Passwoerter.
- [x] JSON-Import mit Auswahl "Ueberschreiben" oder "Vorhandene ueberspringen"
  anbieten. Konflikte anhand von Verknuepfungsname und Windows-Benutzername
  ohne Beachtung der Gross-/Kleinschreibung erkennen.
- [x] Importierte Kontozuordnungen anhand des Benutzernamens wiederherstellen.
  Fehlende lokale Kontokonfigurationen ohne Passwort anlegen; Zugangsdaten
  sind anschliessend separat zu ergaenzen. Keine Windows-Konten erstellen.
- [x] Neuen Typ "Ausfuehrung" als nichtinteraktiven Log-Tab implementieren:
  Programm starten, Standardeingabe schliessen, stdout und stderr fortlaufend
  anzeigen, Exitcode melden. Abbrechen beendet den gestarteten Prozessbaum;
  beim Schliessen laufender Ausfuehrungen bestaetigen lassen.
- [x] Fokus beim Start und Wechsel zu Datei-/Terminal-Tabs explizit auf die
  Dateiliste bzw. Terminaleingabe setzen, auch nach asynchroner Initialisierung.
- [x] Import-/Exportregeln, Reihenfolge, Ausfuehrungslog und reale
  Tastatur-/Fokusablaeufe pruefen und die Release-Ausgabe aktualisieren.

Beim Ueberschreiben bleiben die lokale Verknuepfungs-ID und Listenposition
erhalten. Neue Eintraege werden in der Reihenfolge der JSON-Datei angehaengt.
Kontonamen und Verknuepfungsnamen werden ohne Beachtung der Gross-/Kleinschreibung
verglichen; vorhandene Kontokonfigurationen und gespeicherte Passwoerter bleiben
unveraendert. Ungueltige Importdateien werden vor der Speicherung abgewiesen.
Das aktuelle Windows-Konto wird ueber seine vollstaendige Identitaet zugeordnet;
ein nicht zuordenbares Domaenenkonto kann nicht stillschweigend in ein lokales
Konto umgewandelt werden. Absolute Programm- und Icon-Pfade werden nicht
automatisch auf andere Rechner umgeschrieben.

Der Typ "Ausfuehrung" erfordert eine EXE-Datei. Fuer direkt eingefuegte
PowerShell-Skripte stehen die Typen "PowerShell 5" und "PowerShell 7" zur
Verfuegung; andere Skripte werden ueber ihren Interpreter und Argumente gestartet.
Das Log wird im Tab angezeigt, nicht automatisch als Datei gespeichert.

- [x] Release-Ausgabe erstellen.
- [x] DPAPI, Speicherung, Prozesskommunikation, Dateioperationen und
  PowerShell-Ein-/Ausgabe einschliesslich Terminalgroesse automatisiert pruefen.
- [x] Filterung, Nummerierung, Duplizieren, Abbrechen und Zweizeilenansicht pruefen.
- [x] Reale Tastaturablaeufe im Dateibrowser und im WebView2-Terminal pruefen,
  einschliesslich Tab-Reihenfolge, Ordnernavigation und Loeschbestaetigung.
- [x] Benutzerprozess und Dateiaktionen unter einem gespeicherten lokalen Konto
  mit echten Zugangsdaten pruefen, jeweils mit und ohne Profil-Laden.
- [x] Claude Desktop unter dem gespeicherten Konto aus der Release-Oberflaeche starten.

Beim Start mit Zugangsdaten bleibt der von Windows zurueckgegebene Prozesshandle
erhalten. Er wird nicht verworfen und anschliessend per Prozess-ID neu geoeffnet,
da Windows dies bei einem anderen Benutzer verweigern kann. Der Hilfsprozess
erkennt das Ende des Launchers ueber den Kommunikationskanal, ohne dessen
Prozesshandle unter einem fremden Konto oeffnen zu muessen.

## Build und Laufzeit

Fuer den Build werden .NET 10 SDK und Node.js/npm benoetigt:

```powershell
npm ci
dotnet test Tests\ApplicationStarter.Tests.csproj
node --test Tests\terminal-shortcuts.test.cjs
dotnet publish ApplicationStarter.csproj --configuration Release --no-self-contained
```

Der Integrationstest fuer ein anderes lokales Konto ist standardmaessig
ausgelassen. Er verwendet nur nach ausdruecklicher Aktivierung ein bereits
in der Anwendung gespeichertes Konto und dessen DPAPI-geschuetztes Passwort:

```powershell
$env:APPLICATIONSTARTER_TEST_ACCOUNT = 'LokalerBenutzername'
dotnet test Tests\ApplicationStarter.Tests.csproj --filter FullyQualifiedName~AlternateAccountWorker
```

Dabei wird keine Zugangsinformation als Kommandozeilenargument uebergeben.

Ein separater interaktiver UAC-Test ist standardmaessig ausgeschaltet. Er
benoetigt ein bereits gespeichertes lokales Administratorkonto und eine echte
Windows-UAC-Bestaetigung fuer genau dieses Konto:

```powershell
$env:APPLICATIONSTARTER_TEST_UAC = '1'
$env:APPLICATIONSTARTER_TEST_UAC_ACCOUNT = 'LokalerAdminBenutzername'
dotnet test Tests\ApplicationStarter.Tests.csproj --filter FullyQualifiedName~ConfirmedUac
```

Der Test prueft PowerShell 5 im Log und PowerShell 7 als interaktives Terminal.
Ohne `APPLICATIONSTARTER_TEST_UAC_ACCOUNT` verwendet er das aktuelle Konto,
das dafuer selbst ein Administratorkonto sein muss.

Die Erkennung eines gespeicherten Administratorkontos laesst sich unabhaengig
davon ohne UAC-Dialog mit beiden Windows-Profilmodi pruefen:

```powershell
$env:APPLICATIONSTARTER_TEST_ADMIN_ACCOUNT = 'LokalerAdminBenutzername'
dotnet test Tests\ApplicationStarter.Tests.csproj --filter FullyQualifiedName~SavedAdministratorIsRecognized
```

Startdatei:
`bin\Release\net10.0-windows\publish\ApplicationStarter.exe`.

Die gesamte Publish-Ausgabe zusammen weitergeben. Laufzeitvoraussetzungen sind
.NET 10 Desktop Runtime und fuer Terminal-Tabs Microsoft Edge WebView2 Runtime.
PowerShell 7 muss separat global installiert sein. Zusaetzliche Startkonten
benoetigen Lese- und Ausfuehrungszugriff auf den Programmordner.
