---
run_id: NORD-001
task_id: PM-004
producer: pm
status: ready_for_product_owner_review
revision: 1
---

# Nordwechsel in die Glutklamm

Der neue westliche Nordbogen in Aschenzoll öffnet sich nach persönlich gemeldetem Rattler-Sieg, Ilyans Frachtbericht und vollständiger Beuteaufnahme. Beide Frachtentscheidungen schalten ihn frei. Wer geradeaus geht, erreicht weiterhin den Wald. Im vorhandenen Kapitel 2 kann die Spielfigur den Ember Warden bekämpfen und nach seinem Sieg und dem letzten Beutel Tiefe 3 erreichen. Die nächste Tiefe bleibt beim erneuten Laden erhalten.

| Abnahmekriterien | Ergebnis |
| --- | --- |
| REQ-001, REQ-002 / AC-001, AC-002 | Die reale Portalinteraktion und die getrennten Core-Läufe belegen beide Zielwege sowie die Sperren bei nicht gemeldetem Sieg und offener Ausrüstungs- oder Versorgungsbeute. |
| REQ-003 / AC-003 bis AC-005 | Die Unity-Runtime prüfte mit echten PlayerPrefs die vollständigen Speicherbytes nach Neustart. Ein erzwungener Lesefehler im Kartenkatalog ließ die ursprüngliche Welt und beide Speicherplätze intakt. Beim deterministischen Replay mit Seed 71 wechselte der Welt-Hash zweimal von `2AC47DE508663AE4` auf `7703E8B020A8EB81`. |
| REQ-004, REQ-006 / AC-006, AC-008 | Bestehende Basaltgeometrie, Fissuren, Steg, Dais, Kampfkamera und URP-Volume werden wiederverwendet. Der offene Nordbogen, vier Felsgruppen am Rand und drei skalierte Schiefertöne ergänzen die sichtbare Welt. Beide Kameraprofile, Sicht und Verdeckung am Bogen sowie die drei Materialpfade wurden nativ geprüft. |
| REQ-005 / AC-007 | Acht 100-ms-Ticks gehen dem Warden-Puls mit vier Metern Radius voraus. Der Core prüfte Treffer und Ausweichen, Sieg, Beutesperre und nächste Tiefe. In Unity traf ein regulärer Angriff den Boss. |

Die native Prüfrevision `72510b0c3b4325f3a8928e51bedfdb64262bf7fc` und die GitHub-Produktrevision `2d027d58aee729e537072f4d667a4bee0272b54d` verwenden den Baum `0407a314e9c77df9aefdcbaed8a3e3cf29ef0a04`. Der Vergleich mit `0cf480c6f97e089f23981169d377530d9f06d943` umfasst 28 Produktdateien. Paketmanifest, Paket-Lock und Projekteinstellungen enthalten dieselben Bytes wie vor dem Ausbau.

Unity 6000.6.8f1 mit Android als aktivem Bauziel bestand 16 von 16 ausgewählten PlayMode-Fällen. Das Original-XML hat den SHA-256 `1d3492733c681f9ff1dcdc36f4c94ab76b380db0f5e3cf72fe1df9434cd07821`. Die drei nativen Aufnahmen zeigen Nordbogen, Steg und die Warnung des Warden. Originale Zwischen-XMLs auf 4300ee52 und 4d7fe150 sowie die Vorheraufnahme des Stegs liegen im selben Belegordner. Die netzisolierten Läufe ergaben 23.657 bestandene Core-Assertions, einen Release-Build ohne Warnungen, 64 bestandene Preflight- und 76 bestandene Director-Tests. Integrationsbericht, QC-Fallmatrix und der Belegordner `docs/evidence/nordwechsel-20261126` enthalten die Einzelzuordnung.

Der fehlende Portaleintritt wurde vor der Umsetzung in beiden neuen nativen Fällen reproduziert. Zwei weitere Prüfungen führten zu nachgewiesener Nacharbeit. RPR-001 beseitigte im Kaltstart-Test einen ungleichen Ausgangsseed und -katalog und ergänzte die Sichtlinie am Tor. RPR-002 band das mitgelieferte Schiefermaterial für öffentliche Installationen ein. Ein älterer, vom Nordwechsel unabhängiger Dorftest stößt schon auf der Ausgangsrevision auf die fehlende private Dorfhalle im öffentlichen Checkout. Diese Einschränkung ist im Integrationsbericht auf den konkreten Test begrenzt.

Die drei frischen QC-Records `EVD-3dbba544d962f987`, `EVD-a88a90e75eccd378` und `EVD-5b94a16121327828` bestätigen die unabhängige Nachprüfung. Die Core-Standardausgabe stimmt byteweise mit dem entpackten Nachweis des anderen Rechners überein (SHA-256 `c4a6e96ceb568db79d5147ba8e409ae8e8cc570a8147238d5fe4dc63eea6ea18`). Die Gates für Anforderungen, mobile Lieferung, Integration und QC sind bestanden. Die unabhängige Qualitätsprüfung führt für den Nordwechsel null offene Befunde.

Die technische GitHub-Integration ist vom Auftraggeber freigegeben. Die persönliche Abnahme des Spielgefühls und Messungen der Bildrate auf einem physischen Android-Gerät stehen aus. Der nächste menschliche Schritt ist die Spielabnahme der Passage und der Begegnung im Review-Build. Eine Entscheidung des Product Owners zu dieser Lieferung steht aus.
