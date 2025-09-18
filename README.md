# 📦 BoxLoader (ASN Processor)

A .NET 9 worker service for **processing ASN (Advanced Shipping Notice) files**, persisting them into a SQLite database, and automatically archiving processed files.

---

## ✨ Features

* Watches an **inbox folder** for incoming `.txt` files.
* Parses ASN files (`HDR` and `LINE` records) into strongly typed entities.
* Persists data into SQLite using **Entity Framework Core** and **EFCore.BulkExtensions** for high-volume inserts.
* Prevents duplicate processing using **SHA-256 checksums**.
* Automatically moves successfully processed files into an **archive folder**.
* Handles large files efficiently with batching.
* Fully configurable via `appsettings.json`.
* Unit + integration tests with **xUnit + Shouldly + NSubstitute**.

---

## 🗂 Project Structure

```
src/
  AsnProcessor.Application/   # Application services, parsing, upload logic
  AsnProcessor.Domain/        # Entities: Box, BoxLine, ProcessedFile
  AsnProcessor.Infrastructure # EF Core DbContext, persistence
  AsnProcessor.Worker/        # Worker service, FileWatcher, Program.cs
tests/
  AsnProcessor.Tests/         # Unit & integration tests
```

---

## ⚙️ Configuration

Example `appsettings.json`:

```json
{
  "DataRoot": "data",
  "InboxFolder": "inbox",
  "ArchiveFolder": "archive",
  "DatabasePath": "asn.db",
  "BatchSize": 10000
}
```

* **DataRoot** → base folder where `InboxFolder` and `ArchiveFolder` live.
* **InboxFolder** → where new files are dropped.
* **ArchiveFolder** → where processed files are moved.
* **DatabasePath** → SQLite database file.
* **BatchSize** → bulk insert size.

📂 Final structure after first run:

```
data/
  inbox/    # drop .txt files here
  archive/  # processed files moved here
asn.db      # SQLite database
```

---

## ▶️ Running the Worker

From repo root:

```bash
dotnet run --project src/AsnProcessor.Worker
```

Logs:

```
Watching folder: data/inbox
Database migrated at startup
Application started. Press Ctrl+C to shut down.
```

Drop a `.txt` file into `data/inbox/` → it gets parsed, inserted into DB, and archived.

---

## ⏩ One-off Processing

Process a single file via CLI:

```bash
dotnet run --project src/AsnProcessor.Worker -- --process path/to/file.txt
```

---

## 📄 ASN File Format

Each file contains `HDR` and `LINE` records:

```
HDR  TRSP117                                                                                     6874453I
LINE P000001661                           9781473663800                     12
```

* **HDR** → begins a new box (supplier + identifier).
* **LINE** → adds an item (PO, ISBN, quantity).

---

## 🧪 Tests

Run:

```bash
dotnet test
```

What’s covered:

* ✅ File parsing (HDR + LINE → entities).
* ✅ Upload service (batching, checksum, DB insert).
* ✅ Archiving (file moved after processing).
* ✅ Duplicate content detection (same file only processed once).
* ✅ Archive rename logic (same filename, different content → second file gets timestamp).

---

## 🛠 Development Setup

Prereqs:

* .NET 9 SDK
* SQLite (bundled provider, no extra install)

Build & restore:

```bash
dotnet restore
dotnet build
```

Apply migrations:

```bash
dotnet ef migrations add InitialCreate -p src/AsnProcessor.Infrastructure -s src/AsnProcessor.Worker
dotnet ef database update -p src/AsnProcessor.Infrastructure -s src/AsnProcessor.Worker
```
