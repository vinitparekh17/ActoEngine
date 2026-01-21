# Development Setup

This guide provides instructions for manually setting up the ActoEngine development environment without Docker.

## Prerequisites

- .NET 8.0 SDK or later
- Node.js 18 or later
- SQL Server 2022

## Backend Setup

1.  **Navigate to the Backend directory:**
    ```bash
    cd Backend
    ```

2.  **Restore dependencies:**
    ```bash
    dotnet restore
    ```

3.  **Configure the Database:**
    Update the connection string in `appsettings.json` or set environment variables as defined in the main `README.MD`.

4.  **Run the application:**
    ```bash
    dotnet run
    ```

## Frontend Setup

1.  **Navigate to the Frontend directory:**
    ```bash
    cd Frontend
    ```

2.  **Install dependencies:**
    ```bash
    npm install
    ```

3.  **Start the development server:**
    ```bash
    npm run dev
    ```

## Environment Variables

For manual setup, configure `Backend/.env` with:

- `DB_SERVER` - Database host (default: `127.0.0.1`)
- `DB_PORT` - Database port (default: `1433`)
- `DB_NAME` - Database name (default: `ActoEngine`)
- `DB_USER` - Database user
- `DB_PASSWORD` - Database password (or use `secrets/DB_PASSWORD` for Docker)
- `SEED_ADMIN_PASSWORD` - Initial admin password
- `ASPNETCORE_URLS` - API listen URL (use `http://+:5093` for Docker)
- `VITE_API_BASE_URL` - Frontend API base URL
