# Unused Backend Endpoints Documentation

This document lists backend API endpoints that are not currently used by the frontend application. These endpoints may be:
- Planned for future features
- Available for direct API access
- Candidates for removal if not needed

## Auth Controller

### `/api/Auth/protected` (GET)
- **Status**: Not used
- **Purpose**: Test endpoint for protected routes
- **Recommendation**: Keep for testing or remove if not needed

## User Management Controller

### `GET /api/UserManagement/{userId}`
- **Status**: Not used
- **Purpose**: Get individual user details with permissions
- **Recommendation**: Implement user detail view/modal in frontend

### `PATCH /api/UserManagement/{userId}/status`
- **Status**: Not used
- **Purpose**: Toggle user active/inactive status
- **Recommendation**: Frontend currently uses PUT endpoint - consider using this instead for better REST semantics

### `POST /api/UserManagement/{userId}/change-password`
- **Status**: Not used
- **Purpose**: Change user password
- **Recommendation**: Implement password change functionality in user management

## Role Controller

### `GET /api/Role/{roleId}`
- **Status**: Not used
- **Purpose**: Get individual role details with permissions
- **Recommendation**: Implement role detail view/modal in frontend

## Permission Controller

### `GET /api/Permission`
- **Status**: Not used
- **Purpose**: Get all permissions (flat list)
- **Recommendation**: Frontend uses `/api/Permission/grouped` - this endpoint can be removed if not needed

## Form Builder Controller

### `GET /api/FormBuilder/configs/{projectId}`
- **Status**: Not used
- **Purpose**: Get all form configurations for a project
- **Recommendation**: Implement form config list/management UI

### `DELETE /api/FormBuilder/{id}`
- **Status**: Not used
- **Purpose**: Delete a form configuration
- **Recommendation**: Implement delete functionality in form builder

### `GET /api/FormBuilder/templates`
- **Status**: Not used
- **Purpose**: Get available form templates
- **Recommendation**: Implement template selection UI if templates are needed

## Sp Builder Controller

### `POST /api/SpBuilder/quick/cud`
- **Status**: Not used
- **Purpose**: Quick CUD generation with auto-read schema
- **Recommendation**: Consider implementing quick generation buttons for better UX

### `POST /api/SpBuilder/quick/select`
- **Status**: Not used
- **Purpose**: Quick SELECT generation with auto-read schema
- **Recommendation**: Consider implementing quick generation buttons for better UX

## Context Controller

### `POST /api/projects/{projectId}/context/bulk-import`
- **Status**: Not used (but referenced in UI)
- **Purpose**: Bulk import context entries
- **Recommendation**: Implement bulk import UI (currently disabled in frontend)

### `POST /api/projects/{projectId}/context/review-requests`
- **Status**: Not used
- **Purpose**: Create review request for context
- **Recommendation**: Implement review request workflow if needed

### `GET /api/projects/{projectId}/context/review-requests/pending`
- **Status**: Not used
- **Purpose**: Get pending review requests
- **Recommendation**: Implement review request management UI if needed

### `GET /api/projects/{projectId}/context/statistics/stale`
- **Status**: Not used
- **Purpose**: Get entities with stale context
- **Recommendation**: Dashboard endpoint already includes stale entities - this is redundant

### `GET /api/projects/{projectId}/context/statistics/top-documented`
- **Status**: Not used
- **Purpose**: Get top documented entities
- **Recommendation**: Dashboard endpoint already includes top documented - this is redundant

### `GET /api/projects/{projectId}/context/statistics/critical-undocumented`
- **Status**: Not used
- **Purpose**: Get critical undocumented entities
- **Recommendation**: Dashboard endpoint already includes critical undocumented - this is redundant

## Database Browser Controller

### `GET /api/DatabaseBrowser/projects/{projectId}/structure`
- **Status**: Not used
- **Purpose**: Get database structure (schemas and tables)
- **Recommendation**: Consider using for schema overview if needed

### `GET /api/DatabaseBrowser/projects/{projectId}/tables/{tableName}/schema`
- **Status**: Not used
- **Purpose**: Get table schema by table name
- **Recommendation**: Frontend uses table ID-based endpoints - this can be removed if not needed

### `GET /api/DatabaseBrowser/projects/{projectId}/stored-procedures`
- **Status**: Not used (legacy)
- **Purpose**: Get stored procedures (legacy endpoint)
- **Recommendation**: Frontend uses `stored-procedures-metadata` - remove this legacy endpoint

### `GET /api/DatabaseBrowser/projects/{projectId}/sp-metadata`
- **Status**: Not used (obsolete)
- **Purpose**: Get stored procedure metadata (obsolete)
- **Recommendation**: **REMOVE** - Already marked with [Obsolete] attribute

### `GET /api/DatabaseBrowser/projects/{projectId}/stored-tables/{tableName}/schema`
- **Status**: Not used
- **Purpose**: Get stored table schema by name
- **Recommendation**: Frontend uses table ID-based endpoints - this can be removed if not needed

### `GET /api/DatabaseBrowser/projects/{projectId}/tables/{tableName}/columns`
- **Status**: Not used
- **Purpose**: Get table columns by table name
- **Recommendation**: Frontend uses column ID-based endpoints - this can be removed if not needed

### `GET /api/DatabaseBrowser/tables/{tableId}/stored-columns`
- **Status**: Not used
- **Purpose**: Get stored columns metadata for a table
- **Recommendation**: Frontend uses different endpoint pattern - verify if this is needed

## Client Controller

### `GET /api/Client/{clientId}`
- **Status**: Not used
- **Purpose**: Get individual client details
- **Recommendation**: Implement client detail view if needed

## Summary

### High Priority for Implementation
1. User detail view (`GET /api/UserManagement/{userId}`)
2. Role detail view (`GET /api/Role/{roleId}`)
3. Password change functionality (`POST /api/UserManagement/{userId}/change-password`)
4. Form config management (`GET /api/FormBuilder/configs/{projectId}`, `DELETE /api/FormBuilder/{id}`)
5. Bulk import UI (`POST /api/projects/{projectId}/context/bulk-import`)

### Candidates for Removal
1. `GET /api/Permission` (use grouped endpoint)
2. `GET /api/DatabaseBrowser/projects/{projectId}/sp-metadata` (already obsolete)
3. `GET /api/DatabaseBrowser/projects/{projectId}/stored-procedures` (legacy)
4. Redundant context statistics endpoints (stale, top-documented, critical-undocumented) - dashboard endpoint covers these

### Nice to Have
1. Quick SP generation endpoints (`/api/SpBuilder/quick/cud`, `/api/SpBuilder/quick/select`)
2. Form templates (`GET /api/FormBuilder/templates`)
3. Review request workflow endpoints

