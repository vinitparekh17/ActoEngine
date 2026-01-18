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

For manual setup, ensure you have the following environment variables configured (or provided via a local `.env` file in the root):

- `SA_PASSWORD`
- `DB_NAME`
- `SEED_ADMIN_PASSWORD`
- `VITE_API_BASE_URL`
