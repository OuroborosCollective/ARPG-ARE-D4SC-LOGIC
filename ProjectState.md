# ARE Ashen Crypt — Projektzustand

## Wald, Bärenjunge und sichtbare Ausrüstung (13.11.2026)

[Draft PR31](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/31) ergänzt den Dungeonstand um einen prozeduralen Wald, die feste Bärenhöhle mit dauerhafter Entscheidung und den begehbaren Übergang zur Aschenzoll-Ankunft. 36 vorhandene Waffen-/Schildmodelle sind als echte Beute mit Affixen ausrüstbar und erscheinen am Charakter. [Umfang, Regeln und Reproduktion](docs/WOODLAND_EQUIPMENT.md).

Code **35a93fb227631c930a54406464a05f6a06ea7f3f**: 7860 Core-Assertions,38Preflights, CI43390238851 success; echte Unity6000.6.8f1 PlayMode116/116 und EditMode34/34,0Skips. Private Assets unverändert in Tests, Besitzer-Spielstände und Projekteinstellungen wiederhergestellt. Native Wald-/Bärenhöhlen-/Waffenkatalogansichten geprüft. Vollständige XML-Hashes in Memory.md; native Evidence woodland-runs-20261113/play-stable und edit-stable. Keine neue Android-/Geräteabnahme. PR31 ist auf dem weiterhin offenen PR30 gestapelt; die vollständige Transportermittlung aus PR29 und der spätere Fraktionskrieg sind noch nicht implementiert.

## Prozeduraler Dungeon und Farmruns (13.11.2026)

Der neue Entwicklungsstand liegt in [Draft PR30](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/30): drei verzweigte Ebenen unter der alten Mühle mit variierenden Räumen, Gegnern und optionalen Truhen, Rattler auf Ebene III, bestehende Skills/XP/Level/Mastery und echte Beutebelege. Rückkehr in den Speicher und erneuter Abstieg erzeugt einen neuen Run einschließlich Boss. Brandmal, Ilyans persönlicher Bericht und andere Storyentscheidungen bleiben abgeschlossen. Stockwerkwechsel, Tod und Laden behalten den laufenden Dungeon. Alte AC11-/AC12-Spielstände bleiben lesbar und erhalten einen ausdrücklichen Einstieg am geöffneten Siegel. [Kampf-/Erkundungsvertrag](docs/COMBAT_EXPLORATION.md).

Geprüfter Code cf2a7a96ac84e20253044961b4d1d64d7f6f672d / Tree93be0e1e05865c9f517df6af870460c554fcd4e1: 7308 Core-Assertions,38Preflights, GitHub-CI43362324855 success, native Unity6000.6.8f1 PlayMode113/113 und EditMode34/34,0Skips. Enthalten sind 384 generierte Etagen, zwei volle Farmruns, Ilyan-Bericht, alte Save-Einstiege, Tod, Laden und ungültige Beutebelege. Native Karten-/Spielansichten geprüft; private Assets unverändert, Prefs/Settings wiederhergestellt. SHA-/XML-Bindung: Memory.md [crypt-three-depths-20261113]. Der abschließende Commit ändert nur Dokumentation. Keine neue APK oder physische Android-Abnahme.

Nächster Entwicklungsblock: begehbarer Waldweg von der Mühle nach Aschenzoll mit Kämpfen, Truhen und Bärenhöhle. Deren Entscheidung wird dauerhaft: Felle für eine herzustellende Rüstung oder verschonte Junge mit organisiertem Schutz und möglichem späteren Kriegsbärenvorteil für eine der vier Fraktionen. [Draft PR29](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/29) ist entsprechend angepasst; der direkte Dorf–Stationssprung entfällt. Wald, Bärenquest und Fellrüstung sind inzwischen in PR31 implementiert; der Fraktionskrieg bleibt Zukunftsinhalt. AC13-Ankunftsversuch bleibt geparkt; die folgende Save-Revision muss den AC14-Dungeon erhalten. PR28 ist bereits in main92268e6 gemergt.

## PR28: Reviewkorrekturen und Mergefreigabe (13.11.2026)

Owner-Auftrag: Draft PR28 bearbeiten und zum Merge fuehren. Die bisherige Draft-Bindung ist damit aufgehoben; technische Review- und CI-Gates bleiben erforderlich. PR28 wurde zur automatischen Pruefung freigegeben. Sein Umfang bleibt Fables mobile UI/Kamera, private Welttexturen, Aussenhorizonte, Rattler-Sichtkorrektur sowie Patienten/Muehlenzuflucht/Strassenraender. Der Aschenzoll-Storyentwurf bleibt separat in Draft PR29 und ist kein Bestandteil dieses Merges.

Gepruefter Code ef2926f0e0fe0242492638ae3fdabd9aad0fef37 / Tree8f5489b51948a3cf956e5420d6fd75134288ada8: 1866 Core-Assertions,38Preflights, Unity6000.6.8f1 PlayMode111/111 und EditMode33/33,0Skips. Vier urspruengliche Reviewfehler wurden auf79ac8513907177f8a0f10c2a2abf4f6d9ebf0170 nativ reproduziert; weitere sechs PlayMode- und zwei EditMode-Fehler auf1270dff0437a3d57e35172e656e0e4385f8f4aa3. Der Menue-Panelverdacht bestaetigte sich nicht; die neue Grenzpruefung besteht ohne Layoutaenderung. SHA-/XML-Bindung in Memory.md [pr28-review-merge-20261113]. Private Assets unveraendert, Prefs/Settings wiederhergestellt. Evidence: /var/lib/unityarpg/.local/share/are-arpg/pr28-merge-20261113. Keine neue APK oder physische Android-Abnahme; der vereinbarte Geraeteblock folgt erst nach dem groesseren Story-/Gebietsblock.

Reviewkorrekturen: Helden-Layer nach Portraetansicht wiederherstellen; Legacy-Kameraheading zuruecksetzen; Seitwaertseingabe von automatischem Kamerafolgen entkoppeln; nahe Verdeckung und Mausgesten begrenzen; Minikarte an echte Gebietsausgaenge binden; XP-Balken ohne BigInteger-Ueberlauf; Inventarseiten und Menue-Neulayout bei Viewportwechsel; optionale Weltprefab-Liste und wiederholbare UV-Erzeugung mit Quellnachweis. Nach dem Laden werden explizit zwei Muehlenpatienten verlangt. Die finalen Dokumentationsaenderungen veraendern den getesteten Code nicht. Abschliessender Review-/Mergestatus: [PR28](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/28).

Letzter Nachreviewpunkt: Der neue Autorierungstest musste seinen ignorierten Ausgabeordner auch in einem sauberen Checkout selbst anlegen und nur als eigenen leeren Ordner entfernen. Nativ reproduziert auf01dd3813a261049a8631b97966ec84a03a542ff7; korrigiert auf5cb18744f87a42691de42d60cb4cd148b504001b mit2/2 Tests ohne privates Welt-Overlay und33/33 EditMode mit privatem Bestand,0Skips. Private Dateien unveraendert, Prefs/Settings wiederhergestellt. Runtime-Tree717e5ce25a29ce93d6cd11faf7cecdba6490a4f0 und Editor-Tree28b1c8151dff88826f4df3fa4918dc2703357d97 sind bytegleich zum111/111-PlayMode-Stand ef2926f; keine erneute Laufzeitaenderung.

Der abschliessende Fixturetest reproduzierte ausserdem den Verlust einer bereits auf Platte vorhandenen, noch nicht importierten Datei (REDccef6e2f08c9a6a15598b787d73b47fd4fbeddd6). Die Bestandsaufnahme erfolgt nun nach dem synchronen Import. GREENdf36319e2bf4d13e7a6467664a949719d561543e:34/34 private EditMode-Tests und3/3 gezielte Tests im sauberen Checkout,0Skips; private Assets unveraendert, Prefs/Settings wiederhergestellt. Runtime-/Editor-Quelltrees bleiben identisch; neue Regression betrifft ausschliesslich Testisolation. XML-SHA256 in Memory.md.

Die nachfolgenden datierten Abschnitte bewahren den jeweiligen historischen Reviewstand.

## Entwicklungsblock: MaulwurfRattler (12.11.2026)

- phase: VERTICAL_SLICE / CREATURE_INTEGRATION
- current_goal: Inneren Kryptazugang, blinden MaulwurfRattler, Schleichen und untersuchbares Halsbrandmal als zusammenhaengenden Spielabschnitt integrieren.
- approved: Owner-Kanon zu Aschenglutfällen, konditionierter Geruchsjagd und dem schwebenden Handelsreich Immergruen; gewaehltes Modellpaket Creep Horror Creature. Vollstaendiger Vertrag: [RATTLER_IMMERGRUEN](docs/RATTLER_IMMERGRUEN.md).
- implemented_candidate: Akt 7 mit Rueckweg, lokale Wahrnehmung durch tatsaechliche Schritte/Angriffe und nahen Geruch, fixierte Hiebwarnung, einmalige Belohnung, Brandmaluntersuchung, persoenlicher Bericht an Ilyan und AC12 beim bisherigen Save-Owner. PR25 ist als 86bae8f108aaf1ee508dd7b24dc448e49c2bdc2a in main integriert.
- open_decisions: Konkrete Wappenzeichnung, Abrichter, Auftraggeber, Motiv, konditionierter Zielgeruch und Schicksal der einzelnen Vermissten. Kein persoenlicher Koenigsbefehl allein aus dem Wappen ableiten.
- verified: Privates Creep-Horror-Creature-Originalpaket244853 als CryptRattler gebunden (LOD1, 2107Vertices, vier Clips, Halsbrandmal als Grafikprototyp). Native Knochenbewegung/Brandmalbindung und Begegnung bestanden; Unity6 PlayMode68/68, EditMode31/31. Revisionsbindung und XML-Hashes: Memory.md [rattler-pr25-20261112].
- next: PR28 bleibt Draft zur Sichtabnahme. Aussenhorizonte, Patienten, Muehlenzuflucht und erste Strassenrandgestaltung sind nativ geprueft. Naechsten groesseren Story-/Gebietsblock konkretisieren; weitere visuelle Feinarbeit bleibt offen. Das Brandmal bleibt primaer ein Lore-Hinweis mit optionaler kleiner Grafik; kein neuer Geraetetest vor dem vereinbarten groesseren Story-/Gebietsblock.

## Muehlenzuflucht und Strassenraender (13.11.2026)

Der Schlafbereich hat ein Schutzdach auf vier Holzstuetzen, einen flachen Bretterzugang und Stoffunterlagen. Verbandrollen und ein kleines Versorgungspaket ergaenzen die vorhandene Kiste. Der helle Verband auf dem leeren Bett bleibt auf dunklerem Stoff sichtbar. Zwoelf niedrige Buesche saeumen die Wege; sechs Felsgruppen ausserhalb der Bewegungsgrenze rahmen Strasse und Muehle. Zwei separate passive Buschbindungen stammen aus der bestehenden privaten Dorfszene; deren Original bleibt unveraendert. Editor-Reproduktion: `ARE ARPG/Art/Extract Private Roadside Shrubs`.

Die vier Stuetzen sind echte Hindernisse im integerbasierten Core; Darstellung und Kollision verwenden dieselben Koordinaten und0.24m Breite, mit bestehendem Spielerradius. Hauptweg und beide Untersuchungsstellen bleiben erreichbar. Kein neues Unity-Physiksystem, keine Save-/Quest-/Fable-UI-/Kameraaenderung. Native Gesamt- und Spielansichten gesichtet; bestehender kompletter Regressionstestlauf inklusive Reise-/Save-/Krypta-/Rattlerabschnitten bestanden.

Code6947fadf3d8baf1e923ae05b5e37b2a91c064c32 / Tree a5863fb98f337bf240a654bb902247bbcd359904: Core1866+38Preflights, CI43340748087 success; Unity6000.6.8f1 PlayMode101/101 und EditMode31/31, jeweils0Skips. Reale REDs fuer durchlaufbare Stuetzen und primitive Pflanzen; SHA-/XML-Bindung in Memory.md [mill-refuge-20261113]. Evidence `/var/lib/unityarpg/.local/share/are-arpg/mill-refuge-20261113`, `refined-play`, `refined-edit` und `author-result.json`. Private Dateien in Tests unveraendert, Prefs/Settings wiederhergestellt. PR28 bleibt Draft; keine neue APK oder Android-Leistungsmessung.

## Patienten an der alten Muehle (13.11.2026)

Die Kapseln sind durch ruhende, zugedeckte Bauernfiguren aus den vorhandenen privaten Dorfmodellen ersetzt. Statische Meshes mit5648/5263/5648Vertices, ohne Animator oder zusaetzliche Physik; Quellen unveraendert. Anzahl, Verschwinden und Laden folgen unveraendert dem Core. Native Nahaufnahmen zeigen erst drei Patienten, nach Rueckkehr/Laden zwei und das leere Bett mit Verband. Fable-UI, Spielkamera, Core und Save-Vertrag bleiben erhalten. Reproduktion: `ARE ARPG/Art/Build Private Mill Patients`.

Code665f681f1e91d262f23994f988bd7f0df51759b4 / Tree d9c687de3343fe74c37916dff6daa7bd75511b43: Unity6000.6.8f1 PlayMode100/100 und EditMode31/31, jeweils0Skips, Core1840+38Preflights, CI43336707652 success. Zwei echte REDs fuer fehlende Modelle bzw. fehlende Farbtexturen; finale Autorenerzeugung nutzt die bereits gebackenen Dorftexturen. Nach einmaliger Unity-Importaktualisierung der sechs neuen Patientenmaterialien bestehen die finalen Laeufe mit unveraendertem privaten Bestand und wiederhergestellten Prefs/Settings. SHA-/XML-Bindung: Memory.md [mill-patients-20261113]. Evidence: `/var/lib/unityarpg/.local/share/are-arpg/mill-patients-20261113`, `stable-play` und `stable-edit`; private Authoring-Receipts getrennt. PR28 bleibt Draft; keine neue APK oder Android-Leistungsmessung.

## Aussenhorizonte (13.11.2026)

Dorf, Aschenstrasse und alte Muehle erhalten einen bedeckten Himmel, niedrige Huegel und einen texturierten Uebergang in die Ferne. Die statische Kulisse liegt ausserhalb der Bewegungsgrenze; 910 selbst erzeugte Vertices plus zwei optionale bestehende Felsgruppen, keine Collider und keine eigene Update-Schleife. Der native Test begrenzt die Gesamtgeometrie auf weniger als 30000 Vertices, prueft Shader, Save/Core-Unveraendertheit sowie Entfernung beim Wechsel in den Innenbereich. Fables UI/Kamera und der Core sind unveraendert.

Code e32fd6b3b72e21c780e32e9b56a21de7b56fa4cd / Tree 7af5149addbac53e9024efd575283c5543526f36: Unity6000.6.8f1 PlayMode99/99 und EditMode31/31, jeweils0Skips; Core1840 und38Preflights via CI43333802027 success. Native Dorf-/Strassen-/Muehlenbilder gesichtet; erste zu hohe Silhouette vor diesem Stand abgesenkt. Exakte RED/GREEN-/XML-Bindung: Memory.md [outdoor-horizons-20261113]. Evidence: `/var/lib/unityarpg/.local/share/are-arpg/outdoor-horizons-20261112`. Private Assets unveraendert, Saves/Settings wiederhergestellt. Weiterhin visueller Draft; keine neue begehbare Region, keine Android-Leistungsmessung oder APK-Abnahme. Patientenplatzhalter und weitere Landschaftsdetails bleiben offen.

## Sichtkorrektur am Rattler (12.11.2026)

Die zuvor als Hoehlenverdeckung dokumentierte dunkle Flaeche war das durch importierte Knochenmasse auf rund31m vergroesserte Halsbrandmal. Im isolierten privaten Prefab wurde ausschliesslich dessen lokale Skalierung korrigiert; Kamerafuehrung, UI, Koerpermodell, Animationen und Core bleiben erhalten. Neues natives Bild zeigt freie Sicht auf Rattler und Hiebwarnung. Der Owner versteht das Brandmal primaer als Lore-Hinweis; eine unauffaellige kleine Grafik ist akzeptiert, kein eigenstaendiges Tattoo-Feature erforderlich.

Code6ba7623ecd55ce0fc7e8c0bcf29a126527ead8b7: Core1840, Preflights38, GitHub-CI43331070721 success, native PlayMode98/98 und EditMode31/31,0Skips. Exakte RED/GREEN-/XML-/Prefab-Bindung in Memory.md [rattler-brand-scale-20261112]. Reproduzierbarer Editor-Schritt: `ARE ARPG/Art/Fit Private Rattler Brand`. Private Rohassets bleiben unversioniert. PR28 bleibt Draft zur Sichtabnahme.

## Gemeinsame Integration PR25 / PR28 (12.11.2026)

PR25 ist nach finalen Review-/CI-Gates gemergt und per API sowie Git-Fetch bestaetigt. Der Textur-Draft wurde gegen dieses main zusammengefuehrt. Fables ArenaHudLayout, AshenUiTheme, HudPresentation, MenuPresentation, ArenaCameraRig und HeroCameraPresentation bleiben bytegleich zu PR28; Rattler-Eingabe/HUD bleiben verdrahtet. Beide historischen Memory-Eintraege sind erhalten.

Gemeinsamer nativer Code `18434fca1cca9511bce2cb94685adb6d50c39bb1`: 1840 Core-Assertions, 38 Preflights, GitHub Core-CI43233152350 success, Unity6000.6.8f1 PlayMode98/98 und EditMode31/31 (0Skips), mit privaten Texturen und Creep-Assets. Prefs/Settings wiederhergestellt, private Dateien unveraendert. SHA-/XML-Bindung in Memory.md [integration-pr25-pr28-20261112]. Evidence: `/var/lib/unityarpg/.local/share/are-arpg/integration-20261112`; isolierter Workspace: `/var/lib/unityarpg/workspaces/are-arpg-integrated-20261112`. PR28 bleibt Draft, kein Merge und keine physische Touch-/Android-Abnahme dieses Blocks.

## Historischer Projektzustand vor den Folgeabschnitten

Stand: 2026-11-10 (Aschengrenze-Entwicklung). Repository: github.com/ouroboroscollective/arpg-are-d4sc-logic; PR #1 ist als Merge-Commit e607c609c2ed7f02223689de3aa6cab7aedda3bc auf main.

- phase: VERTICAL_SLICE / GAMEPLAY_EXPANSION
- current_goal: Die gestohlenen Vorraete in der Aschengrenze als echtes AC5-gebundenes Entscheidungsspiel: Maras Lager spenden, Ilyan helfen oder angreifen; persoesnliche Konsequenzen und live sichtbaren Lagerbestand mit einer Autoritaet pruefen.
- approved: Eigenständiges Unity-Android-ARPG; Unity 6000.6.8f1 und offizielle Pipeline 0.10.0-exp.1; integerbasierter 100-ms-Core; Unity projiziert; bestehende AC1/AC2/AC3-Persistenz erhalten. Keine Aurion-Anbindung. Owner erlaubt am 09.11.2026 dauerhaft, seine ausgewählten Account-Pakete bei Bedarf ohne erneute Paketfreigabe zu installieren und zu integrieren. Neuer Owner-Auftrag: kein Levelcap, Lernen durch Tätigkeiten und Gebietsdurchgänge im Spielfluss statt Level-complete-Menüs.
- direction: Director führt technische Planung, Implementierung, Integration und Prüfung aus. Owner entscheidet über prägende Produktfragen, Lore und Art Direction anhand konkreter Alternativen. Die Paketfreigabe ist keine Kauf-, Lizenzzustimmungs- oder Releasefreigabe.
- open_decisions: Die Aschengrenze als Region vom Owner am 10.11.2026 gewaehlt; finale Art-Pakete, grosse Storyzweige und Releases bleiben Owner-Entscheidungen. Physische Spielerabnahme steht aus.
- blocked: Neue Android-APK und physischer Touch-/Save-Test bleiben offen; GitHub-Unity-Workflow skipped (VPS Unity6-PlayMode separat real ausgefuehrt). Reale Android-Geräteabnahme vor dem bestehenden Handoff-Mergegate weiterhin offen. Grafik-/Toolentwicklung wird deshalb nicht auf einen erneuten APK-Handoff verschoben.
- next: Den aktuell geprueften PR8-Reviewfix auf GitHub CI/Reviews/Tree readback pruefen und bei erfuellten Gates mergen; danach AC6 Issue #2 vor Zeugenschaft/Policies #3–#6 implementieren. Aschengrenze-GameView und Charakteranimationen separat pruefen. Inventar-/Skill-Adapter spaeter nur mit bestehendem Core/Save. Praegende Loreentscheidungen mit konkreten Alternativen beim Owner.

## Account-Inhalte und aktuelle Arbeit

Im privaten Unity-6-Intake sind Monster04 (72 Prefabs, 72 PNG) und Skeleton Ghost (4 Prefabs, 19 PNG) tatsächlich importiert. Das ist keine Aussage über fertige Animationen oder Android-Leistung. Die Skelettdateien wurden zusätzlich in den gitignorierten Visual-Root des eigentlichen Spiels übernommen; ihre Physikkomponenten werden nur in separaten Grafik-Kopien entfernt. Vier separate passive Bindings Enemy0 bis Enemy3 wurden mit Unity 6000.6.8f1 erstellt und durch den unveränderten Binder geprüft. Je Vorlage wurden Rigidbody und Collider entfernt, lokale Modellhöhe auf 1,5 Einheiten und Füße auf den Boden der bisherigen Projektion gesetzt. Materialien und Shaderreferenzen bestanden die native Importprüfung. Alle 5 vorhandenen PlayMode-Tests bestanden mit dem privaten Overlay (0 Fehler/Skips). Eine sichtbare Art-Abnahme, abgestimmte Lauf-/Kampfanimationen und Android-Leistung bleiben ungeprüft.

Neun Pakete liegen im privaten Downloadcache, darunter Smart Inventory System, Skill System, 3D Game Kit, Map Tile Grid Creator und Runtime Performance Monitor Lite. Cache, Projektimport und funktionierende Gameplay-Integration sind unterschiedliche Zustände. Inventar-/Skill-Pakete sind hier noch nicht mit dem bestehenden Spielstand integriert. Rohassets bleiben außerhalb dieses öffentlichen Repositorys.

Der erste grafische Intake wurde beim Import durch sein eigenes 4-GB-Speicherlimit beendet. Die Dateien blieben erhalten; der Desktop wurde mit 6-GB-Limit wiederhergestellt. Ein sichtbarer alter Unity-2022-Intake ist kein Nachweis, dass unser Spiel mit 2022 entwickelt wird. Der aktuelle native Grafik-Import nutzt ausschließlich 6000.6.8f1. Alte Installation und Test-APK bleiben erhalten.

## Historischer Prüfabschnitt vom 09.11.2026, 18:47 UTC

Die folgenden Nachweise gelten für ihre ausdrücklich genannten Revisionen. Sie bestätigen nicht automatisch die laufende private Account-Integration.

## Abgeschlossener Schritt und Flächen

Der Director-Teststart am Ausgangshead `178275892d44557e74b6952fa94a15e183eac6e3` wurde real mit `UNTRACKED_GAME_INPUT:Assets/Scenes.meta` blockiert (Exit 2). Die vorhandenen, vom kanonischen `PrepareDemoScene` erzeugten Eingaben wurden geprüft und unter `455bb78079c9f851fec5ca0165c2e5ccd0f6e439` versioniert. Es wurden keine Dateien gelöscht und keine Guard-Ausnahmen hinzugefügt.

| Fläche | Betroffene Eingabe / Wirkung |
| --- | --- |
| Runtime-Projektion | `Assets/Scenes.meta`, `Assets/Scenes/Arena.unity` und deren Meta-Datei; bestehende GUIDs bleiben erhalten. Die leere Szene ist beabsichtigt: ArenaRuntime erzeugt Spielobjekte bei Play. |
| Produktion/Build | `ProjectSettings/EditorBuildSettings.asset` verweist aktiviert auf dieselbe Arena-GUID. |
| Core / Persistenz / Effekte | Keine Änderungen am C#-Core, Runtime-Adapter, Save-Vertrag oder Visual-Bindings. |
| Test | Bestehende 413 Core-Assertions, 21 Preflights und 5 echte PlayMode-Tests erneut ausgeführt. |

Andere Unity-generierte ProjectSettings bleiben außerhalb des versionierten Trees; die lokalen Director-Receipts erfassen ihre SHA-256-Werte. Das ist eine dokumentierte Hosteingabe, kein Nachweis vollständig identischer Einstellungen in jedem frischen Clone.

## Tatsächlich geprüfte Grenzen

| Grenze | Ergebnis und Revision | Quelle |
| --- | --- | --- |
| GitHub / VPS | Beide `455bb78079c9f851fec5ca0165c2e5ccd0f6e439`, Tree `8b57fc540141552518631220b0eec71c9b745269`; alle vier Dateien bytegleich. Tracked clean, untracked_game_inputs leer. | [Code-Commit](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/commit/455bb78079c9f851fec5ca0165c2e5ccd0f6e439) und unabhängiger VPS-Readback. |
| Statisch | 21/21 bestanden, Code-SHA oben. | Director `20261109T184337Z-75ed22c0b13f`; Log-SHA256 `49185373b169218058fa0d10a897e7988375f4fa29eb78afd81f8ba15aba9998`. |
| Core-CI | 413 Assertions + 21 Preflights bestanden; Run-head_sha = Code-SHA. PR-Checkout testet den Merge-SHA `093d6469a5ab5e7633e8deab273ea574d856b7f4` gegen main `259242996b9b9455b86040049b4ab76af4d9b37d`. | [Run 42676510462](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/42676510462), Attempt 1, Job 129359186830, Originaljoblog. |
| Unity-Editor | Unity 6000.6.8f1, 5/5 PlayMode bestanden, 0 Fehler/Skips; Ende 2026-11-09T18:45:05Z. Code-SHA oben. | Director `20261109T184435Z-6867860a60d4`; XML-SHA256 `083e26e28a3ee1ce0020aa2223c4b7020ea68aaba04a1596d60d0917c81caf99`, Unity-Log `c396c13b0e72636fef383d6b696bb519db3648e8af8bab5a67058b6fea5d4e96`. XML, Receipt und Dateien separat gelesen. |
| GitHub Unity | Run 42676510454 übersprungen; kein PASS. | [Run](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/42676510454). Der echte Editorlauf oben ist ein separater lizenzierter VPS-Lauf. |
| Vorhandenes APK / Signatur | Bestehendes ARM64/IL2CPP-APK erneut physisch geprüft: 16.346.460 Bytes, SHA256 `d535d9bc574c09a1a07fbac359acad47ca6f2af9720457b3ccf8fbbaced29a6e`; Debugzertifikat `7b6c7bffa6cc5ff397b317fe10169ab9ba56a2440e90fa19d13308f490377f4a`. | Historischer Build auf `91575837c560efaf4068a188631ab886ed391c8d`; private Buildbelege separat erhalten. Dateiprüfung allein liefert source_commit_sha=null. Kein neuer APK-Build für den Szenencommit und kein Release Candidate behauptet. |
| Gerät / Release | Ungeprüft; ADB-Liste leer. | Keine Tablet-Installation, Touch-/Kamera-Abnahme, Android-Save/Restore oder Store-Promotion aus diesem Arbeitsblock. |

Der eigene MCP-Editor wurde für den Test gezielt beendet und anschließend wieder gestartet (ActiveState active, SubState running). Keine zweite Editorinstanz wurde gestartet. Die separate Android-APK und Unity2022 bleiben erhalten. Headless-Tests liefern keinen visuellen Android- oder Leistungsnachweis.

Diese Datei ist der datierte Produktionszustand. Ältere Verifikationsaussagen in README und Memory sind historische Beobachtungen; der vollständige Dokumentationshead und dessen finale CI werden im privaten Übergabeprotokoll separat festgehalten.

## Private Grafik-Integration — geprüfte Grenze

Source: `8e4cfc6a1d27fe79760636b001c33fe10bad1b41`, dazu privates Overlaymanifest SHA256 `2aa5cf2c3b0490ff80dad8a56be041c64c17e7e9c7bf7072df5c8ec622ddbbc5`. Alle getrackten Source-Dateien waren beim nativen Test unverändert. Unity-PlayMode Ende 2026-11-09T20:55:46Z: 5 bestanden, 0 fehlgeschlagen, 0 übersprungen; XML SHA256 `60f07f12a41481307282bd8f97604a2edba1835b28a58217fc84d9a04a60bf7f`. Private Rezepte, Prefabs und Rohassets sind nicht Teil des öffentlichen Git-Trees. Die drei neuen Ordner-Meta-Dateien halten die GUIDs der vorhandenen Visual-Brücke stabil.

Smart Inventory enthält eigene JSON-/PlayerPrefs-Persistenz sowie automatische UI-Initialisierung. Diese Grenzen wurden im Quelltext gefunden; daraus folgt noch keine kompatible Integration. Der nächste Grafikschritt ist eine echte gerenderte Spielansicht mit den Skeletten und die Auswahl passender vorhandener Animationen/Umgebung. Die Android-Geräteabnahme bleibt ein späterer separater Handoff.

## Handoff-Abgleich und Clone-Korrektur

Die vier zentralen Dateien ArpgSimulation.cs, CharacterProgression.cs, ArenaRuntime.cs und PassiveArtVisuals.cs sind am09.11.2026 tatsächlich bytegleich zur Handoff-Basis57bbf677cfaab4cdad82df33cc4f214abe50e70a geprüft. Kampf/Loot/Kapitel, XP/Mastery/AC3 und Eingabe-/Save-Projektion werden weiterverwendet. Inventar-/Skill-Pakete sind nicht bereits in diese Autorität integriert.

CI42697835110 reproduzierte nach413bestandenen Core-Assertions einen echten Clone-Fehler: Visuals.meta hatte ohne privaten Ordner keinen Zielpfad. Ein öffentlicher README-Anker mit Meta erhält diesen Ordner; sämtliche Account-Inhalte bleiben ignoriert. Keine Prüfung abgeschaltet.

## Learn-by-doing und Weltfluss — neuer Spielblock

Die vier Tätigkeiten Klingenführung, Wucht, Schutz und Ruinmagie wachsen unabhängig durch wirksame Treffer bzw tatsächlich wiederhergestellte HP. BigInteger-XP und Ränge bleiben ungecappt; die endlichen Kampfboni sind im ersten Balancingpass begrenzt. Fehlschläge, Cooldown-Ablehnungen und Overkill erzeugen keinen zusätzlichen Übungsfortschritt. AC4 erweitert den vorhandenen Save-Vertrag; AC1/AC2/AC3 werden ohne Verlust des bisherigen Fortschritts gelesen. Vor dem nativen Test wurde das vorhandene Owner-Profil privat gesichert.

Nach Sieg und Beutesicherung führt der begehbare Nordbogen ins Folgegebiet. Der Core prüft den Abstand, der Runtime-Adapter speichert/readbackt vor dem Wechsel. HP/Fokus/Cooldowns und Fortschritt bleiben erhalten; kurzer Fade statt Abschlussfenster. Kein NextCrypt-Knopf/N-Fernwechsel mehr. Gebietstausch ist implementiert, Open-World-Streaming und vielfältige neue Biome noch nicht.

Source1dde63549cd8ffb8d205d5d716d8a8767ac10f48, Treebff55cf3dd5a7d613f3c15c53b0171e10cde0272:449 deterministische Core-Assertions bestanden,21Preflights bestanden, [Core-CI42701423292](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/42701423292) success. DirectorUnity6-Job20261109T212041Z-5b0ca5aae788 bestand7/7Tests,0Skips, einschließlich echtemClient-Ticking undDurchgangsgeometrie sowieAC4-Roundtrip. XML-SHA25607a43ef09e61c1c77b2b0c8365933a8ed2af688f5f2166fabf200b90db076760. GitHubUnity42701423329 skipped ist keinPASS. Kein neuerAndroid-/sichtbarerArt-/Releasebeleg. Designvertrag: [LEARN_BY_DOING](docs/LEARN_BY_DOING.md).

## Folgeblock — Die gestohlenen Vorräte (10.11.2026)
- `current_goal`: Eine reale Vorratslücke im kanonischen AC5-SocialLedger, eine spielbare Abgabe an Mara und sichtbare kollisionsfreie Lagerhinweise. Keine zweite Quest-/NPC-Authority.
- `approved`: Owner wählte `story.aschengrenze.supplies.v1` als nächsten Konflikt. Bestehender Ilyan-Hilfe-/Angriffspfad und Maras Ressourcenkern werden wiederverwendet.
- `next`: TDD RED/GREEN, echte Unity-6-PlayMode- und .NET-GitHub-CI, unabhängiger Save-/Replay-Readback, visueller GameView, danach getrennt Android-Build/Gerät. AC6 und Zeugenschaft bleiben bei #2–#6.
- `blocked`: Keine Wahrheitsbehauptung zu Täter, Zeugen oder abgeschlossener vollständiger Ermittlungsquest. Android-Device-Gate und finale Art bleiben separat offen.
