# ActoEngine

ActoEngine captures what documentation can't: the "why" behind your database.

## Our Vision

> What if your database could explain itself?

ActoEngine builds a semantic layer that answers the questions your documentation 
doesn’t: “What breaks if I change this?” “Why does this exist?” “Which clients 
depend on this version?”

It maps hidden relationships, detects drift between reality and documentation, 
and simulates change impact before you deploy. The Form Builder and SP Generator 
aren’t just code tools — they’re proof that when systems understand their own 
context, automation becomes intelligent.

## Features

- Database browsing and profiling
- Code generation for stored procedures
- Form builder
- Project management

## Installation

### Prerequisites

- .NET 8.0 or later
- Node.js 18 or later
- PostgreSQL or SQL Server

### Backend

1. Navigate to the Backend directory:
   ```
   cd Backend
   ```

2. Restore dependencies:
   ```
   dotnet restore
   ```

3. Update the connection string in `appsettings.json`.

4. Run the application:
   ```
   dotnet run
   ```

### Frontend

1. Navigate to the Frontend directory:
   ```
   cd Frontend
   ```

2. Install dependencies:
   ```
   npm install
   ```

3. Start the development server:
   ```
   npm run dev
   ```

### Docker Deployment

1. Create a `.env` file from the example:
   ```bash
   cp .env.example .env
   ```

2. Update the `.env` file with your preferred settings.
   The `.env.example` file contains the following variables that you should configure:

   | Variable | Description | Example / Default |
   |----------|-------------|-------------------|
   | `DB_PASSWORD` | **Required.** Password for the SQL Server `sa` user. Must differ from defaults for security. | `your_secure_password_here` |
   | `DB_NAME` | Database name. | `ActoEngine` |
   | `DB_USER` | Database user. | `sa` |
   | `ASPNETCORE_ENVIRONMENT` | Runtime environment. Use `Development` for local dev. | `Development` |
   | `VITE_API_BASE_URL` | URL where the frontend can reach the backend API. | `http://localhost:5093/api` |

3. Build and start the services:
   ```bash
   docker-compose up --build
   ```

4. Access the application:
   - Frontend: `http://localhost:3000`
   - Backend API: `http://localhost:5093`

## Contributing

1. Fork the repository.
2. Create a feature branch.
3. Make your changes.
4. Submit a pull request.

## License

This project is licensed under the MIT License.