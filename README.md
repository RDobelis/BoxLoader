# 📦 BoxLoader

BoxLoader is a worker service that ingests ASN (Advance Shipping Notice) files, parses their contents, stores them into SQLite, and manages file lifecycle (inbox → archive/failed).

---

## ✨ Features

* Watch an **inbox** folder for `.txt` files
* Parse `HDR` (box) and `LINE` (line item) records
* Store parsed data into **SQLite** with **bulk inserts**
* Deduplicate files by **SHA-256 checksum**
* Archive successfully processed files
* Move failed files to a **failed** folder
* Run as a long-lived background service or via CLI (`--process file.txt`)

---

## 🗂 Folder structure

```
src/
  AsnProcessor.Domain/         # Entities (Box, BoxLine, ProcessedFile)
  AsnProcessor.Application/    # Services (UploadService, FileParser)
  AsnProcessor.Infrastructure/ # EF Core DbContext + migrations
  AsnProcessor.Worker/         # Worker host (FileWatcherService, Program.cs)
tests/
  AsnProcessor.Tests/          # Unit and integration tests
```

---

## ⚙️ Configuration (`appsettings.json`)

```json
{
  "DataRoot": "data",
  "InboxFolder": "inbox",
  "ArchiveFolder": "archive",
  "FailedFolder": "failed",
  "DatabasePath": "asn.db",
  "BatchSize": 10000
}
```

* **DataRoot**: base folder for all subfolders
* **InboxFolder**: watched for incoming `.txt` files
* **ArchiveFolder**: successful files moved here
* **FailedFolder**: failed files moved here
* **DatabasePath**: SQLite DB file path
* **BatchSize**: number of boxes per insert batch

---

## 📄 Example ASN File

### Input (`data/inbox/asn1.txt`)

```
HDR  TRSP117                                                                                     6874453I
LINE P000001661                           9781473663800                     12
LINE P000001662                           9781473662179                     5
```

### Parsed Entities in DB

**Box**

| Id | FileId | SupplierIdentifier | Identifier |
| -- | ------ | ------------------ | ---------- |
| 1  | 1      | TRSP117            | 6874453I   |

**BoxLine**

| Id | BoxId | PoNumber   | Isbn          | Quantity |
| -- | ----- | ---------- | ------------- | -------- |
| 1  | 1     | P000001661 | 9781473663800 | 12       |
| 2  | 1     | P000001662 | 9781473662179 | 5        |

---

## 🚀 Running locally

### Prerequisites

* .NET 9 SDK
* SQLite (inbox uses file-based `asn.db`)

### Setup

```bash
git clone https://github.com/your-org/BoxLoader.git
cd BoxLoader

# build solution
dotnet build

# apply migrations
dotnet ef database update --project src/AsnProcessor.Infrastructure --startup-project src/AsnProcessor.Worker
```

### Start the worker

```bash
dotnet run --project src/AsnProcessor.Worker
```

It will watch `data/inbox` by default.

### Process a single file manually

```bash
dotnet run --project src/AsnProcessor.Worker -- --process path/to/file.txt
```

---

## 🧪 Testing

The solution uses **xUnit + Shouldly + NSubstitute**.
Run all tests:

```bash
dotnet test
```

Tests cover:

* ✅ Parsing (`FileParserTests`)
* ✅ Upload workflow (`UploadServiceTests`)
* ✅ Archive + deduplication
* ✅ Failed folder on parse error