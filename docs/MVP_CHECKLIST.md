# MVP-Checkliste

Diese Liste beschreibt den kuerzesten Weg von der aktuellen technischen Basis zu
einem abgeschlossenen, vorzeigbaren Tower-Defense-MVP. Aufgaben werden nach
Abschluss von `- [ ]` auf `- [x]` gesetzt.

Stand des statischen Abgleichs: 2026-09-16. Ein Haken bedeutet, dass der Punkt im
Repository nachweisbar vorhanden ist. Laufzeitverhalten, Balancing, Testlaeufe und
Builds werden erst nach einem erfolgreichen manuellen oder automatisierten Lauf
abgehakt.

## MVP-Ziel

Der MVP ist erreicht, wenn ein Spieler das Spiel ueber die Boot-Szene starten,
ein Kampagnenlevel vollstaendig gewinnen oder verlieren und anschliessend neu
starten oder zum Hauptmenue zurueckkehren kann.

### Geplanter Spielumfang

- [x] Eine vollstaendig spielbare Kampagnenkarte bereitstellen.
- [x] Einen baubaren Tower mit mindestens einem funktionierenden Upgrade anbieten.
- [x] Zwei unterschiedliche Gegnertypen einsetzen.
- [x] Eine Partie auf fuenf feste Wellen begrenzen.
- [ ] Startgeld, Baukosten und Belohnungen sinnvoll konfigurieren.
- [x] Die Partie mit zehn Basisleben starten.
- [x] Fuer erreichte Gegnerziele Basisleben abziehen.
- [x] Nach Abschluss der letzten Welle einen Sieg ausloesen.
- [x] Bei null Basisleben eine Niederlage ausloesen.
- [x] Neustart und Rueckkehr zum Hauptmenue nach Spielende anbieten.
- [ ] Einen eigenstaendig spielbaren Windows-Build erstellen.

## Bereits vorhandene Basis

- [x] `Boot`, `Menu` und `Gameplay` sind in den Build Settings eingetragen.
- [x] Die Szenenwechsel `Boot -> Menu` und `Menu -> Gameplay` sind implementiert.
- [x] Eine Kampagnenkarte (`LevelData_1_1`) und ein Basis-Tower mit Projektil sind als Assets vorhanden.
- [x] Bauen, Geld ausgeben, Gegnerbelohnungen, Verkauf und Tower-Upgrades sind im Code grundsaetzlich vorgesehen.
- [x] Zwei `EnemyData`-Assets (`Basic Enemy` und `Runner`) sind vorhanden.
- [x] Eine EditMode-Testbasis fuer Karte, Pfadfindung, Schwierigkeit, Zielauswahl und Wellenplanung ist vorhanden.

Die vorhandenen Bausteine sind noch nicht zu einem spielbaren Kampagnenlauf
verdrahtet. Insbesondere sind die folgenden Punkte keine Politur, sondern
Voraussetzungen fuer den ersten Smoke-Test.

## 0. Bestehende Gameplay-Basis korrekt verdrahten (P0)

- [x] `MainMenuConfig_Main` in der `Menu`-Szene zuweisen, damit die Kampagnenauswahl Level 1 enthaelt.
- [x] Sicherstellen, dass nach erfolgreichem Laden einer Kampagnenkarte das Spawning beginnt.
- [x] Dabei verhindern, dass im Map-Editor vor einem ausdruecklichen Testlauf Gegner spawnen.
- [x] `EnemyData_Runner` im Runner-Eintrag des `EnemySpawner` zuweisen.
- [x] Mindestens ein `TowerUpgradeData`-Asset erstellen, konfigurieren und im `BuildManager` zuweisen.
- [x] Karte, Pfad, Start, Ziel und baubare Flaechen im normalen Kampagnenmodus sichtbar und unterscheidbar machen.
- [x] Einen Smoke-Test `Boot -> Single Campaign -> Level 1 -> Gegner-Spawn -> Tower bauen -> Gegner besiegen -> Belohnung` erfolgreich durchlaufen.

## 1. Geschlossenen Match-Loop implementieren (P0)

### Verantwortlichkeiten und Konfiguration

- [x] `GameState` als eindeutige Quelle fuer `StartingLives` und `MaxRounds` festlegen und fuer den MVP auf zehn Leben beziehungsweise fuenf Wellen konfigurieren.
- [x] `GameState` als Autoritaet fuer Match-Zustandswechsel und `EnemySpawner` als Verantwortlichen fuer Wellenfortschritt festlegen.
- [x] Einen Neustart fuer den MVP als Reload der `Gameplay`-Szene festlegen.

### GameState

- [x] `GameState` um `Lives` erweitern.
- [x] Eine konfigurierbare Anzahl `StartingLives` hinzufuegen.
- [x] `GameState` um `MaxRounds` erweitern.
- [x] Einen Match-Zustand mit mindestens `Playing`, `Won` und `Lost` einfuehren.
- [x] Ein Event fuer Aenderungen der Basisleben bereitstellen.
- [x] Ein Event fuer das Ende einer Partie bereitstellen.
- [x] Eine Methode zum Beschaedigen der Basis implementieren.
- [x] Sicherstellen, dass Basisleben niemals unter null fallen.
- [x] Methoden beziehungsweise zentrale Logik fuer Sieg und Niederlage implementieren.
- [x] Sicherstellen, dass Sieg oder Niederlage pro Partie nur einmal ausgeloest werden.
- [x] Geldtransaktionen und andere Gameplay-Zustandsaenderungen nach Match-Ende ablehnen.
- [x] Beim Neustart Geld, Runde, Leben und Match-Zustand vollstaendig zuruecksetzen.

### EnemySpawner und Rundenablauf

- [x] Beim Erreichen des Ziels Basisleben abziehen.
- [x] Den Basisschaden pro Gegnertyp konfigurieren und beim Zielereignis anwenden.
- [x] Das Spawning nach einer Niederlage stoppen.
- [x] Nach der letzten konfigurierten Welle keine weitere Runde starten.
- [x] Nach der letzten Welle warten, bis Spawn-Queue und aktive Gegner leer sind.
- [x] Danach den Sieg ausloesen.
- [x] Laufende Gegner bei Neustart oder Rueckkehr zum Menue sauber freigeben.
- [x] Verhindern, dass nach dem Match-Ende noch Belohnungen oder Rundenwechsel auftreten.
- [x] Event-Abonnements beim Stoppen, Neustart und Szenenwechsel sauber loesen.

### Neustart und Laufzeit-Cleanup

- [ ] Bei einem In-Place-Neustart zusaetzlich Tower, Projektile, Ghost/Range-Indikatoren, Grid-Belegung und Pools zuruecksetzen.
- [ ] Bei einem Scene-Reload sicherstellen, dass `GameSession` nur die beabsichtigte Levelauswahl behaelt und Editor-/Test-Flags nicht in die Kampagne durchsickern.
- [ ] Sicherstellen, dass wiederholte Neustarts keine doppelten Events, Gegner oder UI-Instanzen erzeugen.

### HUD und Bedienung

- [x] Aktuelle Basisleben im HUD anzeigen.
- [x] Die Rundenanzeige als `Welle X / Y` darstellen.
- [x] Ein eindeutiges Sieg-Overlay anzeigen.
- [x] Ein eindeutiges Niederlage-Overlay anzeigen.
- [x] Einen funktionierenden `Restart`-Button anbieten.
- [x] Einen funktionierenden `Main Menu`-Button anbieten.
- [x] Gameplay-Eingaben und Tower-Bau nach Match-Ende sperren.
- [x] HUD und Overlays nach einem Neustart korrekt zuruecksetzen.
- [ ] Eine kurze, sichtbare Erklaerung fuer Tower-Auswahl, Platzierung, Abbruch, Upgrade und Verkauf anbieten.

## 2. Tests fuer den Match-Loop (P0)

- [x] Testen, dass ein Gegner am Ziel Basisleben reduziert.
- [x] Testen, dass Basisleben nicht unter null fallen.
- [x] Testen, dass null Basisleben genau einmal `Lost` ausloest.
- [x] Testen, dass die letzte abgeschlossene Welle `Won` ausloest.
- [x] Testen, dass vor der letzten Welle kein Sieg ausgeloest wird.
- [x] Testen, dass nach Match-Ende keine weitere Welle startet.
- [x] Testen, dass nach Match-Ende kein Geld mehr durch spaete Todesereignisse gutgeschrieben wird.
- [x] Testen, dass ein Neustart den vollstaendigen Ausgangszustand wiederherstellt.
- [x] Testen, dass ein Sieg erst ausgeloest wird, wenn nach Welle fuenf sowohl Spawn-Queue als auch aktive Gegner leer sind.
- [x] Einen EditMode-Konfigurationstest fuer die MVP-Szenen und Pflichtreferenzen ergaenzen, damit fehlende Menu-, Runner- oder Upgrade-Assets auffallen.
- [x] Einen PlayMode-Test fuer den Ablauf Gegner-Spawn bis Ziel und Lebensverlust ergaenzen.
- [x] Einen PlayMode-Smoke-Test fuer Laden der Kampagne, Spawning und Match-Ende ergaenzen.
- [x] Alle EditMode-Tests im Unity Test Runner erfolgreich ausfuehren (83/83 am 2026-09-16).
- [x] Alle PlayMode-Tests im Unity Test Runner erfolgreich ausfuehren (4/4 am 2026-09-16).

## 3. Erstes Level balancieren (P1)

- [ ] Zielzeit fuer eine komplette Partie festlegen.
- [ ] Startgeld und Tower-Kosten auf mindestens eine sinnvolle Startentscheidung abstimmen.
- [ ] Tower-Schaden, Reichweite und Feuerrate abstimmen.
- [ ] Kosten und Nutzen des ersten Upgrades abstimmen.
- [ ] Leben, Geschwindigkeit und Belohnungen beider Gegnertypen abstimmen.
- [ ] Anzahl und Zusammensetzung aller fuenf Wellen festlegen.
- [ ] Pausenlaenge zwischen den Wellen und Spawn-Intervalle abstimmen.
- [ ] Sicherstellen, dass Welle 1 ohne Vorwissen verstaendlich und schaffbar ist.
- [ ] Sicherstellen, dass die letzte Welle eine erkennbare Herausforderung darstellt.
- [ ] Sicherstellen, dass mindestens eine schlechte Bauentscheidung noch korrigierbar ist und keine fruehe Sackgasse erzeugt.
- [ ] Das Level mehrfach gewinnen und verlieren, ohne Debug-Eingriffe zu verwenden.

## 4. Spiel-Feedback und Verstaendlichkeit (P1)

- [ ] Treffer visuell oder akustisch erkennbar machen.
- [ ] Den Tod eines Gegners klar darstellen.
- [ ] Das Erreichen des Ziels und den Verlust eines Basislebens hervorheben.
- [ ] Eine kurze Ankuendigung vor jeder neuen Welle anzeigen.
- [ ] Fehlendes Geld beim Bauen oder Upgraden verstaendlich anzeigen.
- [ ] Ungueltige Tower-Platzierung verstaendlich anzeigen.
- [ ] Kauf, Upgrade und Verkauf eines Towers bestaetigen.
- [ ] Start- und Zielzelle sowie die Laufrichtung der Gegner eindeutig kennzeichnen.
- [ ] Platzhaltergrafiken identifizieren und nur die fuer den MVP stoerenden ersetzen.
- [ ] Nicht implementierte, aber im Datenmodell sichtbare Funktionen wie `isPiercing` und `hitAnimator` entweder aus MVP-Inhalten entfernen oder korrekt umsetzen.

## 5. Menue und MVP-Umfang bereinigen (P0)

- [ ] `Single Campaign` als klaren primaeren Spielstart anbieten.
- [ ] Noch nicht implementierte Menuepunkte deaktivieren oder als `Coming Soon` kennzeichnen.
- [ ] `Infinite`, `Challenge`, `Tower Upgrade` und `Options` nicht als fertige Funktionen darstellen.
- [ ] Entscheiden, ob der Runtime-Map-Editor im MVP-Menue sichtbar bleibt.
- [x] Sicherstellen, dass Boot, Menue, Gameplay und Rueckkehr zum Menue durchgehend funktionieren.
- [ ] Beim Verlassen des Gameplays temporaere Session-, Editor- und Testzustaende konsistent bereinigen.
- [ ] Menue und HUD bei kleinen Aufloesungen auf abgeschnittene oder ueberlappende Elemente pruefen.

## 6. Build und MVP-Abnahme (P0)

- [ ] Einen frischen Development-Build aus dem abzunehmenden Commit fuer Windows erstellen; der vorhandene lokale Build vom 2026-05-31 gilt nicht als Nachweis fuer den aktuellen Stand.
- [ ] Den Build ausserhalb des Unity Editors starten.
- [ ] Den Ablauf `Boot -> Menu -> Gameplay` im Build pruefen.
- [ ] Eine Partie im Build gewinnen.
- [ ] Eine Partie im Build verlieren.
- [ ] Neustart nach Sieg und Niederlage pruefen.
- [ ] Rueckkehr zum Hauptmenue pruefen.
- [ ] Mindestens 15 Minuten ohne Exception oder Softlock spielen.
- [ ] Unterschiedliche Bildschirmaufloesungen beziehungsweise Seitenverhaeltnisse pruefen.
- [ ] Pruefen, dass im Player-Log keine relevanten Errors oder fehlenden Referenzen auftreten.
- [ ] Einen finalen Nicht-Development-Build erstellen.

## 7. Repository vor dem MVP-Tag aufraeumen

- [ ] Die lokale Aenderung an `Bullet_Basic.prefab` pruefen und separat committen oder verwerfen.
- [ ] Entscheiden, ob `TextMesh Pro/Examples & Extras` benoetigt wird.
- [ ] Nicht benoetigte TextMesh-Pro-Beispiele in einem separaten Cleanup-Commit entfernen.
- [ ] Sicherstellen, dass keine generierten Build-Dateien eingecheckt werden.
- [ ] Architektur-Dokumentation fuer neue Match-State-APIs und den finalen Match-Flow aktualisieren.
- [ ] Den veralteten `ResetMoney`-Eintrag in `docs/ARCHITECTURE.md` korrigieren oder die API tatsaechlich bereitstellen.
- [ ] Die README-Buildanweisung verifizieren; `dotnet build TD.slnx` schlaegt in der aktuellen Umgebung ohne aussagekraeftigen Compilerfehler fehl.
- [ ] Einen erfolgreichen Testlauf und Build dokumentieren.
- [ ] Den fertigen Stand von `test` nach erfolgreicher Abnahme in `main` uebernehmen.
- [ ] Einen MVP-Tag im Git-Repository anlegen.

## Definition of Done

Der MVP gilt erst als abgeschlossen, wenn alle folgenden Punkte erfuellt sind:

- [ ] Ein neuer Spieler kann ohne Erklaerung eine Partie starten.
- [ ] Bauen, Angreifen, Belohnungen und Upgrades funktionieren im normalen Spielablauf.
- [ ] Die Partie besitzt einen klaren Sieg- und Niederlagezustand.
- [ ] Sieg, Niederlage, Neustart und Hauptmenue funktionieren im Standalone-Build.
- [ ] Es gibt keine bekannten Fehler, die den kompletten Spielablauf blockieren.
- [ ] Alle fuer den MVP vorgesehenen automatisierten Tests sind erfolgreich.
- [ ] Der abgenommene Stand ist committed, in `main` enthalten und als MVP getaggt.

## Nach dem MVP

Diese Punkte sind bewusst nicht Teil des ersten MVPs:

- [ ] Einen zweiten Tower mit eigener taktischer Rolle entwickeln.
- [ ] Eine zweite Kampagnenkarte erstellen.
- [ ] Weitere Tower-Upgrades hinzufuegen.
- [ ] Infinite-Modus implementieren.
- [ ] Challenge-Modus implementieren.
- [ ] Meta- beziehungsweise permanente Tower-Upgrades implementieren.
- [ ] Optionsmenue mit Audio-, Grafik- und Eingabeeinstellungen implementieren.
- [ ] Zusaetzliche Gegner und Gegnereigenschaften entwickeln.
- [ ] Umfangreiches Audio- und Visual-Polishing durchfuehren.
