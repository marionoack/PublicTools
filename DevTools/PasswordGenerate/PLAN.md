# Passwort-Generator — Implementierungsplan

## Überblick

Kompakte Windows-Desktop-Anwendung (.NET 10, WPF) zur schnellen Generierung von Passwörtern verschiedener Längen und Zeichensätze. Feste Fenstergröße, deutsche Oberfläche, eigenes Icon.

---

## Technologie

- **.NET 10** (net10.0-windows)
- **WPF** (XAML, MVVM-Light ohne externes Framework)
- **Sprache:** C#
- **Projekttyp:** Single-Project WPF Application

---

## Fenster-Layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ [Icon] Passwort-Generator           [Neu erzeugen F5] [Ein-/Ausblenden F6] │
├───────────────────────────────────────────────────────────────────────────┤
│              │  8 Zeichen [📋] │ 12 Zeichen [📋] │ 16 Zeichen [📋] │ 20 Zeichen [📋] │
│──────────────┼─────────────────┼─────────────────┼─────────────────┼─────────────────│
│ Zahlen       │ ●●●●●●●●   [📋] │ ●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●●●●●[📋]│
│ Hex          │ ●●●●●●●●   [📋] │ ●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●●●●●[📋]│
│ a-z + 0-9    │ ●●●●●●●●   [📋] │ ●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●●●●●[📋]│
│ A-Z+a-z+0-9  │ ●●●●●●●●   [📋] │ ●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●●●●●[📋]│
│ + Sonderz.   │ ●●●●●●●●   [📋] │ ●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●[📋]│ ●●●●●●●●●●●●●●●●●●●●[📋]│
├───────────────────────────────────────────────────────────────────────────┤
│ Ausschlussliste (Buchstaben): [ 0OoIl1                           ]        │
│ Sonderzeichen:                [ -_#+!%&/()?=                     ]        │
└───────────────────────────────────────────────────────────────────────────┘
```

Spaltenbreiten sind proportional zur Passwortlänge (8* / 12* / 16* / 20*).

### Hinweise zum Layout

- **Jede Zelle** im Grid zeigt ein generiertes Passwort (oder ●●● wenn ausgeblendet)
- **Clipboard-Button** (`[📋]`) hinter **jedem** der 20 Passwort-Felder (nicht pro Zeile)
- Feste Fenstergröße, kein Resizing

---

## Funktionen

### Grid (5 Zeilen × 4 Spalten = 20 Passwörter)

| Zeile | Zeichensatz |
|-------|-------------|
| Zahlen | `0-9` |
| Hex | `0-9, a-f` |
| Kleinbuchstaben + Zahlen | `a-z, 0-9` (abzüglich Ausschlussliste) |
| Groß + Klein + Zahlen | `A-Z, a-z, 0-9` (abzüglich Ausschlussliste) |
| + Sonderzeichen | `A-Z, a-z, 0-9` + konfigurierbare Sonderzeichen (abzüglich Ausschlussliste) |

| Spalte | Passwortlänge |
|--------|---------------|
| 8 | 12 | 16 | 20 |

### Toolbar (oben)

- **Neu erzeugen** (F5): Alle 20 Passwörter neu generieren
- **Ein-/Ausblenden** (F6): Toggle zwischen Klartext und Maskierung (●●●)

### Clipboard

- Hinter **jedem** der 20 Passwort-Felder ein kleiner Copy-Button zum Kopieren in die Zwischenablage

### Einstellungen (direkt sichtbar, unter dem Grid)

- **Ausschlussliste** (TextBox): Zeichen, die bei buchstabenbasierten Passwörtern nicht verwendet werden
  - Vorbefüllung: `0OoIl1`
  - Global für alle Zeilen die Buchstaben enthalten (Zeilen 3-5)
  - Editierbar durch den Benutzer
- **Sonderzeichen** (TextBox): Verfügbare Sonderzeichen für Zeile 5
  - Vorbefüllung: `-_#+!%&/()?=`
  - Editierbar durch den Benutzer

### Passwort-Generierung (Neugenerierung)

- Kryptografisch sicherer Zufallsgenerator (`System.Security.Cryptography.RandomNumberGenerator`)
- **Alle 20 Passwörter werden immer komplett neu erzeugt** — es gibt keine Einzel-Generierung
- Auslöser für Neugenerierung:
  1. **F5** oder Klick auf "Neu erzeugen"
  2. **Jede Änderung** an der Ausschlussliste
  3. **Jede Änderung** an den Sonderzeichen

---

## Projektstruktur

```
PasswordGenerate/
├── PasswordGenerate.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── ViewModels/
│   └── MainViewModel.cs
├── Services/
│   └── PasswordGenerator.cs
├── Resources/
│   └── app.ico
├── PLAN.md
└── .gitignore
```

---

## Implementierungsschritte

### Phase 1: Projekt-Setup
1. .NET 10 WPF-Projekt erstellen (`dotnet new wpf`)
2. `.gitignore` für .NET hinzufügen
3. Icon erstellen/einbinden (in `.csproj` als ApplicationIcon + im XAML als Window.Icon)
4. Fenstergröße festlegen, ResizeMode=NoResize

### Phase 2: Kern-Logik
5. `PasswordGenerator`-Service implementieren
   - Methode: `string Generate(int length, CharacterSet charset, string excludeChars)`
   - Enum/Klasse für die 5 Zeichensatz-Typen
   - `RandomNumberGenerator` für kryptografische Sicherheit

### Phase 3: ViewModel
6. `MainViewModel` mit:
   - 2D-Array (5×4) für die 20 Passwörter
   - Properties: `ExcludeChars`, `SpecialChars`, `IsPasswordVisible`
   - Commands: `RegenerateCommand`, `ToggleVisibilityCommand`, `CopyToClipboardCommand`

### Phase 4: UI
7. `MainWindow.xaml`:
   - Toolbar mit Buttons + KeyBindings (F5, F6)
   - Grid mit 5×4 Passwort-Feldern + je einem Copy-Button
   - Spaltenbreiten proportional (8* / 12* / 16* / 20*)
   - Unterer Bereich mit den zwei TextBoxen (Ausschluss + Sonderzeichen)
   - Passwortfelder: TextBlock (sichtbar) oder maskiert (●●●) je nach Toggle-State

### Phase 5: Feinschliff
8. Icon-Design (einfaches Schloss/Schlüssel-Symbol, 256×256 ICO)
9. Deutsche Beschriftungen überall
10. Tastaturkürzel testen
11. Build & Test als eigenständige .exe

---

## Offene Design-Entscheidungen (bereits geklärt)

| Frage | Entscheidung |
|-------|-------------|
| UI-Framework | WPF |
| Ausschlussliste | Global für alle Buchstaben-Zeilen |
| Vorbefüllung Ausschluss | `0OoIl1` |
| Fenstergröße | Fest (kein Resize) |
| Button-Platzierung | Toolbar oben + Tastaturkürzel |
| Einstellungen | Direkt sichtbar unter dem Grid |

---

## Nicht im Scope

- Speicherung/Persistenz von Einstellungen (ggf. spätere Erweiterung)
- Export/Import von Passwörtern
- Passwort-Stärke-Anzeige
- Multi-Monitor / DPI-Awareness (Standard-WPF-Verhalten reicht)
