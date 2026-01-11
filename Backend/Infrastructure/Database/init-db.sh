#!/bin/bash
set -e

echo "Waiting for SQL Server to be ready..."

# Wait for SQL Server to be available
until /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" -t 1 > /dev/null 2>&1; do
  echo "SQL Server is unavailable - sleeping"
  sleep 2
done

echo "SQL Server is up - executing setup script"

# Read DB_PASSWORD from environment variable or secret file
if [ -f /run/secrets/DB_PASSWORD ]; then
    export DB_PASSWORD=$(cat /run/secrets/DB_PASSWORD)
elif [ -z "$DB_PASSWORD" ]; then
    echo "ERROR: DB_PASSWORD not set and /run/secrets/DB_PASSWORD not found"
    exit 1
fi

# Create a temporary SQL file with substituted variables
# This avoids exposing credentials in process listings via -v flag
TEMP_SQL=$(mktemp)
chmod 600 "$TEMP_SQL"

# Substitute variables in the SQL script
sed -e "s/\$(DB_PASSWORD)/$DB_PASSWORD/g" \
    -e "s/\$(DB_NAME)/$DB_NAME/g" \
    /scripts/setup-user.sql > "$TEMP_SQL"

# Run the setup script
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i "$TEMP_SQL"

# Clean up
rm -f "$TEMP_SQL"

echo "Database user setup completed."
