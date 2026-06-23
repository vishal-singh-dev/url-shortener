# Backend Structure

This folder is prepared for the .NET backend of the Bitly clone using the AWS Lambda infrastructure plan.

## Project

The backend is intentionally kept as a single ASP.NET Core project:

- `src/Api`: Minimal API endpoints, services, models, and infrastructure implementations.

Current folders inside `src/Api`:

- `Models`: core data models such as short links and click events.
- `Services`: request/response contracts, interfaces, and business logic.
- `Infrastructure`: DI registration and current in-memory implementations.

The in-memory infrastructure is temporary. Neon Postgres, Redis, and SQS implementations can replace those classes later behind the existing interfaces.

## Redis / Aiven Valkey

Short-code generation uses Redis when `Redis:ConnectionString` is configured. If it is empty, the API falls back to the in-memory generator for local development.

Supported formats:

- `host:port,password=...,ssl=True,user=default`
- `rediss://default:password@host:port`

For AWS Lambda, set the environment variable as `Redis__ConnectionString`.
