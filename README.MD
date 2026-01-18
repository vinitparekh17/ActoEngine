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

1. **Configure Environment**: Create a `.env` file in the root:
   ```env
   SA_PASSWORD=YourStrong@Passw0rd
   DB_NAME=ActoEngine
   SEED_ADMIN_PASSWORD=Admin@123
   CORS_ALLOWED_ORIGINS=http://localhost:3000
   VITE_API_BASE_URL=http://localhost:5093/api
   ```

2. **Launch**:
   ```bash
   docker-compose up --build
   ```

3. **Access**:
   - Web UI: `http://localhost:3000`
   - API Docs: `http://localhost:5093/swagger`
   - **Default Login**: `admin` / (value of `SEED_ADMIN_PASSWORD`)

---

### Manual Setup & Development
For local development without Docker, please refer to the [Development Guide](./DEVELOPMENT.md).

## Contributing & License
Contributions are welcome! This project is licensed under the MIT License.
