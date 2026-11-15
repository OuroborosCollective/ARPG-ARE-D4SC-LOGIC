# Ankunft in der Aschengrenze — Besitzerentscheidungen vom 15.11.2026

Anlass: Beim Betreten des Dorfes begann die Rationen-Quest sofort mit Entscheidungen. Neue Spieler wussten nicht, worum es geht und welcher Weg welche Folgen hat.

- **Dorf-Intro: A, feste Ankunftsszene.**
  - Beim ersten Betreten läuft eine kurze, nicht überspringbare Szene. Tippen blättert erst nach einer Mindest-Lesezeit weiter.
  - Kamera auf die Sprecher, Untertitel; die Simulation pausiert, die Entscheidungs-Buttons sind verborgen.
  - Danach werden beide Wege erklärt: Mara sofort Ersatz geben, oder herausfinden, wer die Rationen nahm und warum.
  - Verworfen: Gesprächsbäume (B); KI-Gespräche über Convai zur Laufzeit (C). C bräuchte ein Konto, Internet und laufende Kosten und widerspricht der Regel „keine LLM-Aufrufe im Spieltick“.
- **Ilyan: A, verarmter Pfadleser.**
  - Ein ehemaliger Pfadleser, der seit dem Ausbleiben der Händler im Dorf bettelt.
  - Das verbindet die ursprüngliche Bettler-Idee mit dem bestehenden Spieltext.

Szenentext (Core-Vertrag `AshenCrypt.Scene.ArrivalAschengrenze.v1`):
1. Mara: Zwei Rationen fehlen; sie hat am Vorabend jede einzelne gezählt.
2. Dorn: Ein Dieb ist unter ihnen; in Hungerzeiten stiehlt man den anderen das Leben.
3. Mara: Ohne die Vorräte übersteht das Lager den nächsten Ascheregen nicht.
4. Ilyan: Seit die Händler die Wege meiden, braucht niemand einen Pfadleser; er bettelt, ist aber kein Dieb.
5. Dorn: Hunger macht aus jedem einen Dieb, auch aus Ilyan.
6. Mara zum Spieler: Mit Proviant aushelfen oder herausfinden, wohin die Rationen verschwinden, bevor Dorn den Falschen bestraft.

Offen: Bearbeitung der Szene im Fadenwerk-Browser. Der bestehende Fadenwerk-Vertrag hat feste Feldzahlen und einen Hash; eine Szenenerweiterung braucht einen eigenen Gateway- und Editor-Schritt.

## Nachtrag 15.11.2026: Maras Rationen werden nur einmal entschieden

Anlass: „Ration an Mara“ und „Entwenden“ blieben dauerhaft im Menü. Der Besitzer stellte klar: Die Auswahl ist kein dauerhaftes Spielelement, sondern nur für diese Quest-Entscheidung da.

- **Regel:** Über Maras fehlende Rationen wird genau einmal entschieden. Danach verschwinden das Rationen-Menü, seine Touch-Felder und die Tasten G/T dauerhaft.
- **Jeder dieser Wege entscheidet** und wird gespeichert:
  1. Maras Lager vollständig auffüllen. Eine einzelne Teilspende entscheidet noch nichts.
  2. Eine Ration aus dem Lager entwenden.
  3. Ilyan befragen. Damit beginnt die Untersuchung.
- Die Hauptgeschichte geht auf jedem Weg weiter. Auch nach dem Auffüllen oder dem Diebstahl kann Ilyan befragt werden.
- Vor dem Ende der Ankunftsszene ist keiner der Wege möglich. Der Core antwortet „Hör zuerst zu, was im Lager geschehen ist.“
- Spätere Versuche lehnt der Core ohne Wirkung ab: „Über Maras fehlende Rationen ist bereits entschieden.“
- Ältere Spielstände: Ein schon volles Lager oder eine schon begonnene Untersuchung gilt als Entscheidung. Die Szene wird dann nicht nachgeholt.
- Das alte Arena-Dorf (Kapitelmodus ohne Story-Profil) behält sein offenes Lager. Die Regel gilt im Story-Dorf.
- Questlog: „Über Maras fehlende Rationen entschieden“ ersetzt „Nebenaufgabe: Maras Lager aufgefüllt“.
