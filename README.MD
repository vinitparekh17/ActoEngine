# ActoEngine

ActoEngine captures what documentation can't: the "why" behind your database.

## Our Vision

> What if your database could explain itself?

ActoEngine builds a semantic layer that answers the questions your documentation 
doesn’t: “What breaks if I change this?” “Why does this exist?”

It maps hidden relationships, detects drift between reality and documentation, 
and simulates change impact before you deploy. The Form Builder and SP Generator 
aren’t just code tools — they’re proof that when systems understand their own 
context, automation becomes intelligent.

## Key Features

- **Semantic Context**: Document the "why" behind database entities with drift detection.
- **Impact Analysis**: Visualize dependencies and simulate change impact before deployment.
- **Smart Automation**: Generate Stored Procedures and Forms using schema context.
- **Entity Explorer**: Unified browsing for tables, SPs, views, and functions.
- **RBAC & Security**: Fine-grained permissions, audit logging, and secure authentication.

## Tech Stack

**Backend:** .NET 8.0, ASP.NET Core, Dapper, SQL Server/Postgres  
**Frontend:** React 18, TypeScript, Vite, TanStack Query, Shadcn UI  
**Infrastructure:** Docker, Nginx, JWT

## Quick Start (Docker)

The fastest way to get ActoEngine running is using Docker Compose.

1. **Create Secret Files** (passwords stored securely):
   ```bash
   # Create secrets directory if not exists
   mkdir -p secrets
   
   # Create password files (no trailing newline)
   echo -n "YourStrong@Passw0rd" > secrets/SA_PASSWORD
   echo -n "YourAppPassword123!" > secrets/DB_PASSWORD
   ```

2. **Configure Environment**: Edit `Backend/.env`:
   ```env
   DB_NAME=ActoEngine
   SEED_ADMIN_PASSWORD=Admin@123
   ```

3. **Launch**:
   ```bash
   docker-compose up --build
   ```

4. **Access**:
   - Web UI: `http://localhost:3000`
   - API Docs: `http://localhost:5093/swagger`
   - **Default Login**: `admin` / (value of `SEED_ADMIN_PASSWORD`)

---

### Manual Setup & Development
For local development without Docker, please refer to the [Development Guide](./DEVELOPMENT.md).

## Contributing & License
Contributions are welcome! This project is licensed under the MIT License.
