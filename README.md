# Docs Validator

A comprehensive .NET minimal API application for validating, managing, and approving PDF documents with a workflow-based system, digital signature validation, and malware scanning.

## Features

- **User Authentication & Authorization**: JWT-based authentication with role-based access control
- **Role-Based Access Control**: Administrator, Validator, and Expert roles with specific permissions
- **PDF Document Management**: Upload, download, and store PDF documents securely
- **File Validation**:
  - Extension validation (PDF only)
  - Digital signature validation
  - ClamAV antivirus scanning
  - Secure file naming to prevent exploits
  - File hash calculation for integrity verification
- **Workflow Management**: Multi-step workflow for document approval and validation
- **Permission System**:
  - Scopes: CanRead, CanWrite, CanDelete, CanUpdate, CanValidate
  - Permissions: All, OnlyHis, Assigned
- **REST API**: Comprehensive API endpoints for all operations
- **Database**: Entity Framework Core with SQL Server

## Project Structure

```
DocsValidator/
├── Models/
│   └── Models.cs                 # Domain models
├── Data/
│   └── ApplicationDbContext.cs    # EF Core DbContext
├── Services/
│   ├── AuthenticationService.cs   # JWT token generation and password hashing
│   ├── AuthorizationService.cs    # Permission verification
│   ├── ValidationServices.cs      # File validation, signature, ClamAV
│   ├── WorkflowService.cs         # Workflow management
│   └── DocumentStorageService.cs  # Document upload/download/storage
├── Endpoints/
│   ├── AuthenticationEndpoints.cs # Login/Register endpoints
│   ├── DocumentEndpoints.cs       # Document management endpoints
│   ├── WorkflowEndpoints.cs       # Workflow endpoints
│   └── AdminEndpoints.cs          # Administrative endpoints
├── Program.cs                      # Application startup and configuration
├── appsettings.json               # Configuration settings
└── DocsValidator.csproj           # Project file
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB or full version)
- Optional: ClamAV server for virus scanning

### Installation

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd docs_validator
   ```

2. **Install dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure the database connection:**
   Edit `appsettings.json` and update the connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=DocsValidator;..."
   }
   ```

4. **Apply migrations:**
   ```bash
   dotnet ef database update
   ```

5. **Configure JWT settings:**
   Update `appsettings.json` with your JWT secret (minimum 32 characters):
   ```json
   "Jwt": {
     "SecretKey": "your-super-secret-key-change-this-in-production...",
     "Issuer": "DocsValidator",
     "Audience": "DocsValidatorApi"
   }
   ```

6. **Configure file storage:**
   Ensure the storage path exists:
   ```json
   "FileStorage": {
     "Path": "./storage"
   }
   ```

7. **Optional - Configure ClamAV:**
   If using ClamAV for virus scanning:
   ```json
   "ClamAV": {
     "Url": "http://localhost:3310"
   }
   ```

### Running the Application

```bash
dotnet run
```

The API will be available at `https://localhost:7001`

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login and get JWT token
- `GET /api/auth/me` - Get current user information

### Documents
- `POST /api/documents/upload` - Upload a PDF document
- `GET /api/documents/{documentId}` - Get document details
- `GET /api/documents/{documentId}/download` - Download document
- `GET /api/documents` - List user's documents
- `GET /api/documents/{documentId}/validation-status` - Get validation status

### Workflows
- `GET /api/workflows/{workflowId}` - Get workflow details
- `GET /api/workflows` - List user's workflows
- `POST /api/workflows/{workflowId}/assign-validator` - Assign validator
- `POST /api/workflows/approvals/{approvalId}/approve` - Approve workflow
- `POST /api/workflows/{workflowId}/reject` - Reject workflow
- `GET /api/workflows/{workflowId}/status` - Get workflow status

### Administration
- `GET /api/admin/users` - Get all users
- `GET /api/admin/users/{userId}` - Get user details
- `POST /api/admin/users/{userId}/deactivate` - Deactivate user
- `POST /api/admin/users/{userId}/activate` - Activate user
- `GET /api/admin/workflows` - Get all workflows
- `GET /api/admin/documents` - Get all documents

## User Roles & Permissions

### Administrator
- All:CanRead
- All:CanWrite
- All:CanDelete
- All:CanUpdate
- All:CanValidate

### Validator
- Assigned:CanRead
- Assigned:CanUpdate
- Assigned:CanValidate

### Expert
- OnlyHis:CanRead
- OnlyHis:CanUpdate
- OnlyHis:CanDelete
- OnlyHis:CanWrite
- OnlyHis:CanValidate

## Workflow Process

1. **User Authentication**: User logs in to the system
2. **Document Upload**: User uploads a digitally signed PDF and assigns a validator
3. **Workflow Initiation**: System creates a workflow and initiates validation
4. **File Validation**:
   - Extension validation (must be PDF)
   - ClamAV antivirus scanning
   - Digital signature validation
5. **Approval Assignment**: System notifies assigned validator
6. **Validator Review**: Validator downloads, reviews, and signs the document
7. **Approval**: Validator approves or rejects the document
8. **Completion**: If approved, workflow is completed; if rejected, process stops

## Database Schema

### Main Tables
- **Users**: User accounts and roles
- **Documents**: Uploaded PDF documents and metadata
- **Workflows**: Document approval workflows
- **WorkflowSteps**: Individual steps in a workflow
- **WorkflowApprovals**: Approval assignments and status
- **RolePermissions**: Role-based permissions

## Security Considerations

1. **JWT Tokens**: Secure token-based authentication
2. **Password Hashing**: BCrypt hashing for password security
3. **File Validation**: Extension and content validation
4. **Antivirus Scanning**: ClamAV integration for malware detection
5. **Secure File Names**: Generated UUIDs prevent filename exploits
6. **Authorization**: Role and permission-based access control
7. **HTTPS**: HTTPS redirection in production

## Configuration

Key configuration items in `appsettings.json`:

```json
{
  "Logging": { ... },
  "ConnectionStrings": { ... },
  "Jwt": {
    "SecretKey": "...",      // Change in production
    "Issuer": "...",
    "Audience": "..."
  },
  "FileStorage": {
    "Path": "./storage"      // Document storage location
  },
  "ClamAV": {
    "Url": "..."             // ClamAV server URL (optional)
  }
}
```

## Error Handling

The API returns appropriate HTTP status codes:
- `200 OK`: Successful request
- `201 Created`: Resource created
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Authentication failed
- `403 Forbidden`: Access denied
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

## Logging

Logging is configured in `appsettings.json`. Services log important operations:
- Workflow creation and status changes
- Document uploads and validations
- User authentication
- Authorization decisions
- Errors and exceptions

## Integration with ClamAV

To enable antivirus scanning:

1. Set up ClamAV server or use Docker:
   ```bash
   docker run -d -p 3310:3310 clamav/clamav:latest
   ```

2. Update `appsettings.json` with ClamAV URL:
   ```json
   "ClamAV": {
     "Url": "http://localhost:3310"
   }
   ```

## Performance Considerations

- **Database Indexing**: Indices on frequently queried fields (Username, Email)
- **File Storage**: Large files stored on disk, not in database
- **Async Operations**: All I/O operations are asynchronous
- **Query Optimization**: EF Core `Include()` for eager loading

## Testing

To test the API endpoints, use:
- Postman
- curl
- Swagger UI (available at `/swagger` in development)
- dotnet test (for unit tests)

### Example: Register and Login

```bash
# Register
curl -X POST https://localhost:7001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"user1","email":"user1@example.com","password":"password123","role":"Expert"}'

# Login
curl -X POST https://localhost:7001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"user1","password":"password123"}'
```

## License

This project is provided as-is for administrative and educational purposes.

## Support

For issues, questions, or feature requests, please contact the development team.
