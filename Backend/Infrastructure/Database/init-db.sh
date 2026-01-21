#!/bin/bash
set -e

echo "Waiting for SQL Server to be ready..."

# Validate DB_NAME is set and non-empty
if [ -z "$DB_NAME" ]; then
    echo "ERROR: DB_NAME is not set or empty"
    exit 1
fi

# Read SA password from secret file or environment variable
if [ -f /run/secrets/SA_PASSWORD ]; then
    MSSQL_SA_PASSWORD=$(cat /run/secrets/SA_PASSWORD)
    export MSSQL_SA_PASSWORD
elif [ -z "$MSSQL_SA_PASSWORD" ]; then
    echo "ERROR: MSSQL_SA_PASSWORD not set and /run/secrets/SA_PASSWORD not found"
    exit 1
fi

# Wait for SQL Server to be available
until /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" -t 1 > /dev/null 2>&1; do
  echo "SQL Server is unavailable - sleeping"
  sleep 2
done

echo "SQL Server is up - executing setup script"


# Read DB_PASSWORD from environment variable or secret file
# Split assignment and export to avoid SC2155
if [ -f /run/secrets/DB_PASSWORD ]; then
    DB_PASSWORD=$(cat /run/secrets/DB_PASSWORD)
    export DB_PASSWORD
elif [ -z "$DB_PASSWORD" ]; then
    echo "ERROR: DB_PASSWORD not set and /run/secrets/DB_PASSWORD not found"
    exit 1
fi

# Escape backslashes in password first (\ -> \\), then single quotes (' -> '') for SQL string literals
DB_PASSWORD_ESCAPED="${DB_PASSWORD//\\/\\\\}"
DB_PASSWORD_SQL="${DB_PASSWORD_ESCAPED//\'/\'\'}"

# Create a temporary SQL file with substituted variables
# This avoids exposing credentials in process listings via -v flag
TEMP_SQL=$(mktemp)
chmod 600 "$TEMP_SQL"

# Trap to ensure temp file is cleaned up on EXIT (success or failure)
trap 'rm -f "$TEMP_SQL"' EXIT

# Substitute variables in the SQL script using | as delimiter to avoid sed injection
# Also escape \ and & in replacement strings to prevent sed special character issues
# We must escape backslashes again for sed because sed consumes one level of escaping
DB_PASS_SED_ESCAPED="${DB_PASSWORD_SQL//\\/\\\\}"
DB_PASS_SED_ESCAPED="${DB_PASS_SED_ESCAPED//&/\\&}"

sed -e "s|\\\$(DB_PASSWORD)|${DB_PASS_SED_ESCAPED}|g" \
    -e "s|\\\$(DB_NAME)|${DB_NAME//&/\\&}|g" \
    /scripts/setup-user.sql > "$TEMP_SQL"

# Run the setup script with -b flag to fail on SQL errors
/opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -b -i "$TEMP_SQL"

echo "Database user setup completed."
