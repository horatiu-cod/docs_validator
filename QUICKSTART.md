# Quick Start Guide

## Setup

1. **Prerequisites**
   - .NET 8.0 SDK
   - SQL Server (LocalDB or full)
   - Visual Studio, VS Code, or command line

2. **Clone & Setup**
   ```bash
   git clone <repo>
   cd docs_validator
   dotnet restore
   ```

3. **Configure Database & Email**
   - Edit `appsettings.json`:
     - Update connection string if not using LocalDB
     - Update JWT secret (minimum 32 characters)
     - Set file storage path
     - Configure Email settings (SMTP or external provider). Example:

       ```json
       "Email": {
         "Provider": "Smtp",
         "Smtp": {
           "Host": "smtp.example.com",
           "Port": 587,
           "UseTls": true,
           "Username": "your-email@example.com",
           "Password": "app-password",
           "FromAddress": "noreply@yourdomain.com",
           "FromName": "Docs Validator"
         },
         "Retries": 3,
         "RetryDelaySeconds": 60,
         "TimeoutSeconds": 30
       }
       ```

     - Store SMTP credentials securely (User Secrets for local dev or Azure Key Vault in production).

4. **Create Database**
   ```bash
   dotnet ef database update
   ```

   Optional: configure the notifications recipient for approved documents in `appsettings.json`:

   ```json
   "Notifications": {
     "ApprovedRecipient": "approvals@yourdomain.com"
   }
   ```

5. **Run Application**
   ```bash
   dotnet run
   ```
   - API: https://localhost:7001
   - Swagger: https://localhost:7001/swagger

## Test Users

Create these users via the API:

```bash
# Administrator
POST /api/auth/register
{"username":"admin","email":"admin@example.com","password":"Admin123!","role":"Administrator"}

# Validator
POST /api/auth/register
{"username":"validator","email":"validator@example.com","password":"Validator123!","role":"Validator"}

# Expert
POST /api/auth/register
{"username":"expert","email":"expert@example.com","password":"Expert123!","role":"Expert"}
```

## Test Workflow

1. **Login as Expert**
   ```bash
   POST /api/auth/login
   {"username":"expert","password":"Expert123!"}
   ```
   Save the `token` from response

2. **Upload Document**
   ```bash
   POST /api/documents/upload
   Headers: Authorization: Bearer {token}
   Body: multipart/form-data with PDF file
   ```
   Save the `workflowId`

   Note: When a workflow is initiated the document owner will receive an email notification (if email is configured).

3. **Assign Validator**
   ```bash
   POST /api/workflows/{workflowId}/assign-validator
   Headers: Authorization: Bearer {token}
   Body: {"validatorId":"<validator-user-id>"}
   ```

   Note: The assigned validator will receive an email notification with the assignment details.

4. **Login as Validator**
   - Get validator token

5. **Approve Workflow**
   ```bash
   POST /api/workflows/approvals/{approvalId}/approve
   Headers: Authorization: Bearer {validator-token}
   Body: {"comment":"Document approved"}
   ```

   Note: When the workflow completes the document owner will receive a notification email.

## Key Concepts

### Roles
- **Administrator**: Full access to all resources
- **Validator**: Can approve assigned documents
- **Expert**: Can manage own documents

### Workflow Statuses
- Pending: Workflow created
- Validating: File validation in progress
- AwaitingApproval: Waiting for validator approval
- Approved: Document approved
- Rejected: Document rejected
- Signed: Document signed
- Completed: Workflow finished

### File Security
1. Extension checked (PDF only)
2. Filename generated (UUID)
3. ClamAV scanned (if configured)
4. Digital signature validated
5. Hash calculated for integrity

## Troubleshooting

### Database Connection Issues
- Check connection string in `appsettings.json`
- Verify SQL Server is running
- Check firewall settings

### JWT Token Errors
- Verify secret key is configured
- Check token hasn't expired (24 hours)
- Ensure Authorization header format: `Bearer {token}`

### File Upload Issues
- Verify file is PDF only
- Check file size < 100MB
- Ensure storage directory exists

### ClamAV Errors
- If not using ClamAV, files will pass scan
- To enable: Run ClamAV server and update `appsettings.json`
- Docker: `docker run -d -p 3310:3310 clamav/clamav:latest`

## API Reference

### Authentication
- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Get token
- `GET /api/auth/me` - Current user

### Documents
- `POST /api/documents/upload` - Upload PDF
- `GET /api/documents` - List documents
- `GET /api/documents/{id}` - Get details
- `GET /api/documents/{id}/download` - Download
- `GET /api/documents/{id}/validation-status` - Check scan

### Workflows
- `GET /api/workflows` - List workflows
- `GET /api/workflows/{id}` - Get details
- `POST /api/workflows/{id}/assign-validator` - Assign
- `POST /api/workflows/approvals/{id}/approve` - Approve
- `POST /api/workflows/{id}/reject` - Reject
- `GET /api/workflows/{id}/status` - Check status

### Administration
- `GET /api/admin/users` - List users
- `POST /api/admin/users/{id}/deactivate` - Deactivate
- `POST /api/admin/users/{id}/activate` - Activate
- `GET /api/admin/workflows` - All workflows
- `GET /api/admin/documents` - All documents

## Development Tips

1. **Use Swagger UI** for interactive API testing
2. **Check logs** in console for debugging
3. **Database migrations** are automatic on startup
4. **Storage directory** is created automatically
5. **JWT payload** visible at jwt.io

## Production Deployment

1. Change JWT secret in `appsettings.json` or environment variable
2. Use SQL Server (full version recommended)
3. Set `ASPNETCORE_ENVIRONMENT=Production`
4. Enable HTTPS with valid certificate
5. Configure ClamAV for security scanning
6. Set up regular database backups
7. Monitor logs and error rates
8. Consider load balancing for scale

## Support

For issues or questions, check:
- README.md for detailed documentation
- API logs in console
- Database for records
- ClamAV logs if scanning
