# Fadenwerk 1.1.0 – Quests, Dialoge und Belohnungen für Unity 6

Editor-Plugin mit Browser-Oberfläche (auch für Unity auf einem VPS) und Offline-Laufzeit für dein Spiel.

**Start:** Window → Fadenwerk → Datenbank anlegen → Server starten → Link kopieren und im Browser öffnen.

Die ausführliche Anleitung (Installation, Zugriff vom VPS, Arbeiten in der Oberfläche, Einbindung ins Spiel)
liegt als `ANLEITUNG.md` im Download-Archiv.

- `Runtime/` – Komponenten und Spiel-Logik (offline, keine Abhängigkeiten). Lizenz: MIT, siehe `Runtime/LICENSE.md`.
- `Editor/` – Unity-Fenster, Web-Server im Editor, Browser-Oberfläche. Lizenz: siehe `Editor/LICENSE.md`.

**Projekt-Erweiterungen:** Ein Projekt kann über `FadenwerkAuthoringBridge` eine eigene Browser-Seite
(`ExtensionPageKey` + `ExtensionPage`, ausgeliefert unter `/?<key>`) und eigene API-Aufrufe unter `/api/x/…`
(`Extension`, läuft im Unity-Hauptthread, auch im Play-Modus) anmelden. Beides nutzt denselben Server,
denselben Zugangsschlüssel und denselben geschützten Proxy. Beispiel: der Bau-Modus von Ashen Crypt (`/?bau`).
