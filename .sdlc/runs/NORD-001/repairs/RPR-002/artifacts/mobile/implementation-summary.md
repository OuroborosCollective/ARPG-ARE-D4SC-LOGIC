---
run_id: "NORD-001"
task_id: "MOB-001"
producer: frontend
status: draft
revision: 2
---

# Nordwechsel und Glutklamm

Abgenommene Produktrevision `4d7fe150f7c7cd9532ee7cfcd846b3394382a1c2`, aufbauend auf `0cf480c6f97e089f23981169d377530d9f06d943`.

| Anforderungen | Umsetzung und Nachweis |
| --- | --- |
| REQ-001, REQ-002 · AC-001, AC-002 | Aschenzoll hat einen westlichen Nordbogen bei −9,5 m / 18 m. Die normale Passage verlangt den früher persönlich gemeldeten Rattler-Sieg, Ilyans persönlichen Frachtbericht und vollständig eingesammelte Bodenbeute. Beide Frachtentscheidungen öffnen den Bogen. Geradeaus bleibt die optionale Waldreise erreichbar. Die bestehenden Gebietsausgänge verhindern das Mitnehmen eines offenen Beutezustands bis zur Station. |
| REQ-003 · AC-003 bis AC-005 | Eine Kopie des gelesenen Charakters erhält die Region Glutklamm und die nächste freigegebene Kryptatiefe. Fracht, Rattler-Bericht, optionale Waldreise, Begleiter, Waffenmeisterschaft und bestehende AC22-Abschnitte überstehen den Kaltstart. Die Runtime schreibt und liest den Zielcheckpoint über ihre bestehenden PlayerPrefs-Slots, bevor sie die aktive Welt tauscht. Der native PlayMode-Lauf prüfte echten Kaltstart, identische AC22-Bytes und den abgewiesenen Weltwechsel bei Speicherfehlern. |
| REQ-004, REQ-006 · AC-006, AC-008 | Die Kapitelprojektion nutzt den vorhandenen Steg, Ritualdais, vier Kampfpfeiler und sieben Fissuren. Der Nordbogen nutzt bestehende Bogen-, Schiefer- und Feuerschalenelemente. Vier Schieferdetails stehen außerhalb der Spielfläche. Die Kampfkamera berücksichtigt den Pulsradius und manuelles Zoomen. Das vorhandene URP-Volume schaltet die Kapitelabstimmung um. Paketmanifest und Engine-Abhängigkeiten bleiben auf der Ausgangsrevision. |
| REQ-005 · AC-007 | Der bestehende Ember Warden kämpft mit seinem 4-m-Impuls. Ein begonnener Impuls läuft über acht 100-ms-Ticks auch dann ab, wenn die Figur die Aggroreichweite verlässt. Der reguläre Angriff auf Warden und beide Wächter erzeugt reale Beute. Erst nach dem letzten Beutel und am nördlichen Ausgang wechselt die Figur von Tiefe 2 in Tiefe 3. |

## Prüfläufe

Der Testpatch `fafe51782e45d876b001321ff6d66cb4953026d5c211deeece88dbcecd6b808d` schlug vor der Integration lokal und auf dem getrennten Prüfrechner nach dem erfolgreichen Frachtbericht am fehlenden Übergang fehl. Ein zweiter Regressionstest reproduzierte den zuvor stehenbleibenden Puls nach dem Verlassen der Aggroreichweite.

`dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release` mit .NET SDK 8.0.428 bestand lokal 23.657 Prüfungen. Zwei aus demselben Stationscheckpoint unabhängig initialisierte Läufe mit Seed 71 lieferten beide den Quellenhash `2AC47DE508663AE4` und den Zielhash `7703E8B020A8EB81`. Der bereits gelesene Quellenzustand behielt Hash und Tiefe. Der vollständige Kaltstart, der stärkere Warden auf Tiefe 3 und der persönliche Berichtsstatus wurden im Core erneut geprüft.

## Reparatur und nativer Nachlauf

Auf der ersten Produktrevision `4300ee522de4917bc025f429b12fa721af5e05d1` bestanden elf von zwölf Unity-Prüfungen. Beim Kaltstart-Hashvergleich nutzte die vorbereitete Station einen anderen Seed und Beutekatalog als die ausgelieferte Einstiegsszene. Der Wechsel zum Produktionsseed band drei weitere Identitäten in der Forge-Population. Die Reparatur `168a5a7bfdb0734beb0810ed1383bc3b60386581` beginnt und beendet den Test mit echtem Laden von AshenEntry und prüft zusätzlich die vollständigen gespeicherten AC22-Bytes. Die Kameraprüfung kontrolliert freien Durchblick unter dem Nordbogen und die Verdeckung durch einen steinernen Pfeiler.

Auf `4d7fe150f7c7cd9532ee7cfcd846b3394382a1c2` bestand der wiederholte Core-Lauf alle 23.657 Prüfungen. Unity 6000.6.8f1 (`237b6593792b`) bestand alle 13 ausgewählten nativen PlayMode-Prüfungen, ohne Fehler oder übersprungene Prüfung. Der Aufnahmehelfer nutzt den tatsächlichen Gameplaykamerakontext. SHA-256 des nativen XML-Berichts `515ef826ed9cad0da5d2d316db550ae14fbcc3375ef8d9ec5c3f59525bea5588`.

Eine Messung von Bildrate oder Framezeit auf einem physischen Android-Gerät steht aus. Die C#-Szenenprüfungen liegen unter `Assets/Tests/PlayMode`. Der Integrationsbericht führt die tatsächlich auf dem Unity-Runner ausgeführten Prüfungen samt Revision auf.
