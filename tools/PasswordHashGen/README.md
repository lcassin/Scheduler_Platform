# Password Hash Generator

This tool generates PBKDF2 password hashes for the Scheduler Platform development users.

## Usage

### From Command Line (Recommended)

```bash
cd tools/PasswordHashGen
dotnet run
```

The tool will:
1. Generate password hashes for all three dev users
2. Display them in the console
3. Wait for you to press Enter before closing

### From Visual Studio

1. Right-click the `PasswordHashGen` project
2. Select "Set as Startup Project"
3. Press F5 or click Run
4. The console window will stay open until you press Enter

## What It Does

Generates PBKDF2 hashes for these dev users:
- `dev-admin@cassinfo.com` / `DevAdmin!2025!!`
- `dev-editor@cassinfo.com` / `DevEditor!2025!!`
- `dev-viewer@cassinfo.com` / `DevViewer!2025!!`

## Next Steps

After running this tool:

1. **Copy the generated hashes** from the console output
2. **Update SETUP_DEV_USERS.sql** in the repository root:
   - Replace `PLACEHOLDER_HASH_FOR_DevAdmin!2025!!` with the hash for dev-admin
   - Replace `PLACEHOLDER_HASH_FOR_DevEditor!2025!!` with the hash for dev-editor
   - Replace `PLACEHOLDER_HASH_FOR_DevViewer!2025!!` with the hash for dev-viewer
3. **Update RESET_DEV_PASSWORDS.sql** with the same hashes
4. **Run SETUP_DEV_USERS.sql** against your DEV/UAT database

## Important Notes

- **Different hashes each time**: The tool generates different hashes each run due to random salts. This is normal and secure.
- **Copy carefully**: Make sure to copy the entire hash string (usually starts with `AQAAAA...`)
- **Don't commit hashes**: The SQL scripts with placeholders are in the repo. After you add real hashes, don't commit them if they contain sensitive passwords.

## Troubleshooting

**Console closes immediately when double-clicking the EXE:**
- Don't double-click the built executable
- Always run from a terminal using `dotnet run`
- Or run from Visual Studio with F5

**"The type or namespace name 'Identity' does not exist":**
- Run `dotnet restore` in the PasswordHashGen directory
- Make sure .NET 10 SDK is installed
