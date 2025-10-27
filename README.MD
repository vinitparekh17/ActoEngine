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

## Usage

1. Start the backend API.
2. Start the frontend application.
3. Access the application at `http://localhost:5173`.

## Contributing

1. Fork the repository.
2. Create a feature branch.
3. Make your changes.
4. Submit a pull request.

## License

This project is licensed under the MIT License.