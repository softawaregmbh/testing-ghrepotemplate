# TestingGhrepotemplate

TODO: Readme ausfüllen

[![CodeScene Code Health](https://codescene.softaware.at/{id}/status-badges/code-health)](https://codescene.softaware.at/{id}/analyses/latest)

Die Anwendung Demo-Projekt für die Firma Demo Solutions dient zur Verwaltung von Daten und Abbildungen einem komplexen System.

## Issue Tracking
- [ ] [Azure DevOps](https://...)

## Aktive MitarbeiterInnen
Ziel: 2 Personen
* Anna Architektin
* Erich Entwickler

## Kontaktpersonen
**Max Mustermann**\
CTO Demo Solutions\
max.mustermann@demo.at\
0664/12345678

**Susi Musterfrau**\
Projektleiterin\
susi.musterfrau@demo.at\
0664/87654321

## Betrieb
Die Anwendung läuft in der Azure-Subscription von Demo Solutions in der Resource Group [demo-project](https://...).
* [DEV-System](https://...)
* [TEST-System](https://...)
* [PROD-System](https://...)

### Zugangsdaten
EntwicklerInnen von softaware erhalten die notwendigen Berechtigungen, indem sie Mitglied in der Entra Gruppe `demo-project-dev` werden.
Die Anmeldung in der Anwendung erfolgt über Auth0. Zugangsdaten für einen Demo-Benutzer sind im Key Vault hinterlegt.

### Build & Deployment (Version ausrollen)
Deployment erfolgt über den GitHub-Actions-Workflow [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml)
mittels `aspire deploy`. Ausgelöst wird er durch:

* **Push auf `main`** – jeder Merge deployt
* **manuell** über "Run workflow" (nur von `main`, durch die Branch-Beschränkung des Environments)
* **wöchentlich** (Montag 02:00 UTC) als Wartungsvertrags-Build

Dadurch wird eine neue Version auf den **Staging-Slot** deployed. Das Swappen erfolgt manuell über das Azure-Portal.
Der Slot-Swap ist damit das eigentliche Freigabe-Gate – nicht der Workflow.

#### Environments
Das Suffix der Azure-Ressourcennamen (`...-testingghrepotemplate-prod`) kommt aus
`aspire deploy --environment <name>`: Das Flag setzt `builder.Environment.EnvironmentName`, den
`TestingGhrepotemplateResourceNameResolver` in das Suffix übersetzt (`Production` → `prod`,
`Development` → `dev`, sonst der Name unverändert). Ohne Flag greift der Default `Production`, deshalb liefert
auch ein blankes `aspire publish` die `-prod`-Artefakte.

Ein zusätzliches Environment braucht daher **keine** Code-Änderung – nur ein GitHub Environment, einen Federated
Credential und den passenden Wert für `--environment` (siehe Kommentar in `deploy.yml`). Umgekehrt gilt: Ein
Tippfehler im Environment-Namen legt einen vollständigen, leeren Parallel-Stack an, statt zu scheitern.

#### Datenbank-Migrationen
`aspire deploy` migriert die Datenbank automatisch, **bevor** die neue Version live geht. Der Ablauf ist in
`TestingGhrepotemplate.AppHost/DatabaseMigrationExtensions.cs` verdrahtet:

1. Aspire generiert beim Deploy ein idempotentes EF-Core-SQL-Skript.
2. Ein Azure Deployment Script (`migrate-database.bicep`) führt dieses Skript gegen die Azure-SQL-Datenbank aus.
   Es läuft als **Managed Identity des SQL-Admins** – der einzigen Identität mit DDL-Rechten. Die Anwendungs-Identität
   benötigt daher keine schema-ändernden Berechtigungen.
3. Erst danach wird das neue Image gepusht und auf den Staging-Slot deployed (`provision-migrate-database` →
   `push-backend` → `provision-backend-website`). Der Image-**Build** läuft parallel zur Migration.

**Voraussetzung für Zero-Downtime:** Migrationen müssen abwärtskompatibel im **Expand/Contract**-Muster geschrieben
sein. Die alte, noch laufende Version (Production-Slot) muss mit dem bereits migrierten Schema weiterarbeiten können.
Breaking Changes (z. B. Spalte umbenennen/löschen) daher auf mehrere Releases aufteilen:
zuerst additiv erweitern (Expand), Code umstellen, erst in einem späteren Release aufräumen (Contract).

Das ist hier keine Empfehlung, sondern Pflicht: Der wöchentliche Workflow-Lauf migriert die Produktions-Datenbank
ohne menschlichen Auslöser, während die alte Version im Production-Slot weiterläuft.

**Seeding:** Statisches Seeding über `HasData()` funktioniert, da es Teil des generierten SQL-Skripts wird.
`UseSeeding()` / `UseAsyncSeeding()` funktioniert **nicht** – diese laufen als C#-Code beim Start des DbContext,
nicht als DDL im Migrations-Skript, und werden vom Deployment Script nicht ausgeführt.

**Slot-Swap:** Nach erfolgreichem Deployment und Smoke-Tests auf dem Staging-Slot wird manuell geswappt
(Azure-Portal oder `az webapp deployment slot swap`).

#### Smoke-Tests
* Anmeldung mittels Demo-Benutzer aus dem Key Vault möglich
* Dashboard zeigt Daten an

### Logging
Logs werden in [Application Insights](https://...) gespeichert.

## Lokales Setup

### Backend
1.`backend\TestingGhrepotemplate.slnx` öffnen

### Frontend
1. `frontend` in VS Code öffnen
2. `npm install`
3. [Kendo-Lizenz aktivieren](https://www.telerik.com/kendo-angular-ui/components/my-license)
4. `npm run start`

### Proxy-Generierung

Dieses Projekt nutzt eine [CQS-Architektur](https://github.com/softawaregmbh/library-cqs), auf Basis derer automatisch eine API und TypeScript-Proxies generiert werden.

Um Funktionalität hinzuzufügen, müssen nur Requests (Commands oder Queries) und deren Handler im `TestingGhrepotemplate.Cqs.Contract` Projekt hinzugefügt werden. Eine ASP.NET API für diese Requests wird automatisch mittels einem [C# source generator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) (`TestingGhrepotemplate.Cqs.Api.Generator`) im Projekt `TestingGhrepotemplate.Cqs.Api` erzeugt.

Basierend auf der [Open API](https://spec.openapis.org/oas/latest.html)-Spezifikation werden durch ein Post-Build Event im `TestingGhrepotemplate.Cqs.Api` Projekt mittels [nswag](https://github.com/RicoSuter/NSwag) TypeScript Proxies generiert.

## Project Fitness Checklist
- [ ] Projekt-Budget in Time Cockpit erfasst
- [ ] Slack Channel in Time Cockpit eingetragen
- [ ] Verwendung von Conventional Commits
- [ ] Pull Requests und Code Reviews für alle Änderungen (Branch Protection)
- [ ] Verwendendung von Infrastructure as Code 
- [ ] Automatisierte Tests vorhanden
- [ ] Aktiver Wartungsvertrag
  - [ ] Regelmäßige Builds: z.B. wöchentlich
  - [ ] Azure Support Plan
  - [ ] Health Endpunkt mit Alerts
  - [ ] Nutzung von Dependabot

## SLAs aus Wartungsvertrag
Kunde erstellt Tickets in HappyFox
* bei Priorität "Hoch": Antwort innerhalb von 4 Stunden zu Geschäftszeiten
* sonst Antwort innerhalb von zwei Werktagen
