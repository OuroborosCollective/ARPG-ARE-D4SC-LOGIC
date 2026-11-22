# Abnahme: Dorfhäuser mit eigenen Räumen (21.11.2026)

Geprüfter Code: `1dcff0f` auf `main` `7c45d79`. Unity 6000.6.8f1, Editor mit Software-Renderer (keine GPU).

## Was abgenommen wurde

| Kriterium | Nachweis | Ergebnis |
|---|---|---|
| Hineinlaufen in die Haustür führt in den Raum, die Tür in der Rückwand wieder vor das Haus | Core `ThroughEveryDoorAndBack`, PlayMode `HousesShowTheirRoomsAsTheCoreHasThem` | bestanden |
| Mit gehaltenem Stick wird jede Tür genau einmal durchschritten | Core `held-on-its-heading-the-player-passes-each-door-once`; zuerst rot (Tür in der Südwand), dann grün | bestanden |
| Wände und Möbel halten, alles im Raum ist erreichbar | Core `Layout` | bestanden |
| Spielstand aus einem Raum ist gültig und lädt vor dem Haus; Begleiter wartet draußen | Core `ThroughEveryDoorAndBack`, `TheCompanionWaitsOutside` | bestanden |
| Türen bleiben zu, solange ein Dorfbewohner feindlich ist | Core `ABarredDoor` | bestanden |
| Raum zeigt genau die Bestände des Kerns, Lampen, Feuer nur bei Arbeit oder Pflege, Liegende im Bett | PlayMode `HousesShowTheirRoomsAsTheCoreHasThem` | bestanden |
| Feld zeigt den Stand der Saat | PlayMode, derselbe Test | bestanden |
| Raummodell und Regeln passen zusammen | EditMode `PrivateRoomsHoldTheirThingsWhereTheRulesHaveTheirFurniture`; mit den alten Modellen rot, mit den neuen grün | bestanden |

Arbeitende und Liegende wurden im PlayMode-Test als Kernzustand von Hand gesetzt; der Kern lief währenddessen nicht.

## Gesamtläufe auf `1dcff0f`

| Lauf | Ergebnis |
|---|---|
| Core-Prüfung | 22.196 Prüfungen bestanden |
| Preflight, Unity-Vertrag, Director, Fadenwerk-Gateway | 54 bestanden, Vertrag PASS, 59 bestanden, 5 bestanden |
| EditMode | 239 von 243 bestanden, 0 fehlgeschlagen, 4 übersprungen (brauchen eigene Testdaten) |
| PlayMode | 191 von 191 bestanden |

## Nicht geprüft

- Android-Build und Gerät.
- Bildrate im Raum auf einem Gerät mit GPU.
