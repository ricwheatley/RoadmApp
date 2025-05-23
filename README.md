# RoadmApp

RoadmApp is a sample .NET 8.0 web application that demonstrates how to
connect to the Xero API using OAuth 2.0 and ingest data from various Xero
endpoints into a PostgreSQL database.  It provides a simple control panel for
managing Xero connections, manually triggering data synchronisation and
configuring scheduled polling.

## Features

- OAuth 2.0 authentication and token storage using Xero's SDK.
- Manual and scheduled polling of Xero accounting and asset endpoints.
- Raw JSON responses are stored in a configurable PostgreSQL schema.
- Background service that checks tenant polling schedules and runs due polls.
- Basic dashboard for viewing organisations, triggering syncs and reviewing
  load logs.
- Unit tests using xUnit.

## Requirements

- .NET 8 SDK
- PostgreSQL database.  Provide a connection string via the
  `POSTGRES_CONN_STRING` environment variable or `ConnectionStrings:Postgres`
  in configuration.
- Xero app credentials (client id and secret) configured under the
  `XeroConfiguration` section of `appsettings.json`.

## Building and Running

```bash
# restore packages and build
dotnet build RoadmApp.sln

# run the web app
cd XeroNetStandardApp
dotnet run
```

Navigate to `https://localhost:5001` and follow the prompts to connect a Xero
organisation.  Once connected you can trigger data loads from the control panel
and configure daily or weekly polling schedules.

## Tests

Run the unit tests with:

```bash
dotnet test
```

## License

This project is licensed under the MIT License.  See the [LICENSE](LICENSE)
file for details.
