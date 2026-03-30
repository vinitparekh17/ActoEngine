using System.Security.Cryptography;
using System.Text;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatchApplyScriptBuilder
{
    string Build(string zipFilePath);
}

internal sealed class PatchApplyScriptBuilder : IPatchApplyScriptBuilder
{
    public string Build(string zipFilePath)
    {
        var resolvedZipPath = PatchPathSafety.NormalizePath(zipFilePath, nameof(zipFilePath));
        if (!File.Exists(resolvedZipPath))
        {
            throw new FileNotFoundException("Patch zip file was not found.", resolvedZipPath);
        }

        var zipFileName = Path.GetFileName(resolvedZipPath);
        var expectedHash = ComputeSha256(resolvedZipPath);
        var escapedZipFileName = EscapeBatVariable(zipFileName);
        var escapedExpectedHash = EscapeBatVariable(expectedHash);

        return $$"""
@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul 2>&1

REM ============================================================
REM  ActoEngine Patch Apply Script
REM  This script verifies the patch ZIP, backs up existing files,
REM  and extracts the patch to the target directory.
REM ============================================================

REM --- Fix working directory (handles "Run as administrator" defaulting to System32) ---
cd /d "%~dp0"

REM --- Remove Mark of the Web (Zone.Identifier) to prevent SmartScreen blocking ---
if exist "%~f0:Zone.Identifier" (
    del /f "%~f0:Zone.Identifier" >nul 2>&1
)

REM --- Admin rights check (informational only) ---
net session >nul 2>&1
if !errorlevel! neq 0 (
    echo [WARNING] Not running as Administrator. If the target folder requires
    echo           elevated permissions, right-click this script and choose
    echo           "Run as administrator".
    echo.
)

REM --- Locate the patch ZIP next to this script ---
set "ZIP_FILE={{escapedZipFileName}}"
set "ZIP_PATH=%~dp0!ZIP_FILE!"

if exist "!ZIP_PATH!:Zone.Identifier" (
    del /f "!ZIP_PATH!:Zone.Identifier" >nul 2>&1
)

if not exist "!ZIP_PATH!" (
    echo [ERROR] Patch ZIP not found next to the script: !ZIP_PATH!
    echo         Make sure both this script and the ZIP file are in the same folder.
    goto :fail
)

REM --- Verify patch ZIP integrity using SHA256 (certutil is available on Windows 7+) ---
set "EXPECTED_HASH={{escapedExpectedHash}}"

where certutil >nul 2>&1
if !errorlevel! neq 0 (
    echo [ERROR] certutil was not found on this system.
    echo         This tool is required to verify the patch file integrity.
    goto :fail
)

set "HASH_TEMP=%TEMP%\acto_patch_hash_%RANDOM%%RANDOM%.txt"
certutil -hashfile "!ZIP_PATH!" SHA256 > "!HASH_TEMP!" 2>&1
if !errorlevel! neq 0 (
    echo [ERROR] Failed to compute SHA256 hash of the patch ZIP.
    del /f "!HASH_TEMP!" >nul 2>&1
    goto :fail
)

set "ACTUAL_HASH="
for /f "skip=1 tokens=* delims=" %%h in (!HASH_TEMP!) do (
    if not defined ACTUAL_HASH (
        set "ACTUAL_HASH=%%h"
    )
)
del /f "!HASH_TEMP!" >nul 2>&1

REM certutil outputs hash with spaces (e.g. "A1 B2 C3 ..."), remove them
set "ACTUAL_HASH=!ACTUAL_HASH: =!"

if /i "!ACTUAL_HASH!" neq "!EXPECTED_HASH!" (
    echo [ERROR] Patch ZIP hash mismatch.
    echo         Expected: !EXPECTED_HASH!
    echo         Actual:   !ACTUAL_HASH!
    echo         The file may be corrupted or tampered with.
    goto :fail
)
echo [OK] Patch file integrity verified.

REM --- Resolve target path (passed as argument or defaults to current directory) ---
set "TARGET_PATH=."
if "%~1" neq "" (
    set "TARGET_PATH=%~1"
)

for %%i in ("!TARGET_PATH!") do set "TARGET_PATH=%%~fi"

if not exist "!TARGET_PATH!" (
    echo Creating target directory: !TARGET_PATH!
    mkdir "!TARGET_PATH!" 2>nul
    if !errorlevel! neq 0 (
        echo [ERROR] Cannot create target directory. Check permissions.
        goto :fail
    )
)

REM --- Create backup directory with locale-safe timestamp ---
set "TIMESTAMP="
for /f "tokens=2 delims==" %%a in ('wmic os get localdatetime /value 2^>nul ^| find "="') do (
    set "DT=%%a"
    set "TIMESTAMP=!DT:~0,4!!DT:~4,2!!DT:~6,2!_!DT:~8,2!!DT:~10,2!!DT:~12,2!"
)

if not defined TIMESTAMP (
    set "TIMESTAMP=%RANDOM%_%RANDOM%"
)

set "BACKUP_DIR=!TARGET_PATH!\backup_!TIMESTAMP!"
echo Creating backup at: !BACKUP_DIR!
mkdir "!BACKUP_DIR!" 2>nul
if !errorlevel! neq 0 (
    echo [ERROR] Cannot create backup directory. Check permissions.
    goto :fail
)

REM --- Read ZIP entries and back up files that would be overwritten ---
set "ENTRY_LIST=%TEMP%\acto_patch_entries_%RANDOM%%RANDOM%.txt"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "try { Add-Type -AssemblyName System.IO.Compression.FileSystem; $z = [IO.Compression.ZipFile]::OpenRead('!ZIP_PATH!'); $z.Entries | Where-Object { $_.Name -ne '' } | ForEach-Object { $_.FullName }; $z.Dispose() } catch { exit 1 }" > "!ENTRY_LIST!" 2>&1

if !errorlevel! neq 0 (
    echo [WARNING] Could not read ZIP entries for per-file backup. Skipping backup phase.
    echo           The extraction will still proceed.
    del /f "!ENTRY_LIST!" >nul 2>&1
    goto :extract
)

for /f "usebackq tokens=* delims=" %%e in ("!ENTRY_LIST!") do (
    set "ENTRY=%%e"
    set "ENTRY_FIXED=!ENTRY:/=\!"

    REM --- Path traversal safety check ---
    echo !ENTRY_FIXED! | findstr /c:"..\\" >nul && (
        echo [ERROR] Unsafe archive entry detected: %%e
        del /f "!ENTRY_LIST!" >nul 2>&1
        goto :fail
    )
    echo !ENTRY_FIXED! | findstr /r "\.\.$" >nul && (
        echo [ERROR] Unsafe archive entry detected: %%e
        del /f "!ENTRY_LIST!" >nul 2>&1
        goto :fail
    )
    echo !ENTRY_FIXED! | findstr /c:":" >nul && (
        echo [ERROR] Unsafe archive entry detected: %%e
        del /f "!ENTRY_LIST!" >nul 2>&1
        goto :fail
    )

    set "TARGET_FILE=!TARGET_PATH!\!ENTRY_FIXED!"

    REM --- Verify resolved path stays under target root ---
    for %%r in ("!TARGET_FILE!") do set "RESOLVED_TARGET_FILE=%%~fr"
    echo !RESOLVED_TARGET_FILE! | findstr /i /b "!TARGET_PATH!" >nul
    if !errorlevel! neq 0 (
        echo [ERROR] Resolved path escapes target root: %%e
        del /f "!ENTRY_LIST!" >nul 2>&1
        goto :fail
    )

    if exist "!TARGET_FILE!" (
        set "BACKUP_FILE=!BACKUP_DIR!\!ENTRY_FIXED!"

        REM --- Verify backup path stays under backup root ---
        for %%r in ("!BACKUP_FILE!") do set "RESOLVED_BACKUP_FILE=%%~fr"
        echo !RESOLVED_BACKUP_FILE! | findstr /i /b "!BACKUP_DIR!" >nul
        if !errorlevel! neq 0 (
            echo [ERROR] Resolved path escapes backup root: %%e
            del /f "!ENTRY_LIST!" >nul 2>&1
            goto :fail
        )

        for %%b in ("!BACKUP_FILE!") do (
            if not exist "%%~dpb" mkdir "%%~dpb" 2>nul
        )
        echo   Backing up: !ENTRY_FIXED!
        copy /y "!TARGET_FILE!" "!BACKUP_FILE!" >nul 2>&1
    )
)

del /f "!ENTRY_LIST!" >nul 2>&1

REM --- Extract the patch ZIP ---
:extract
echo.
echo Extracting patch files to: !TARGET_PATH!

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "try { Expand-Archive -LiteralPath '!ZIP_PATH!' -DestinationPath '!TARGET_PATH!' -Force } catch { Write-Host '[ERROR] Extraction failed:' $_.Exception.Message; exit 1 }"

if !errorlevel! neq 0 (
    echo [ERROR] Extraction failed. Your backup is at: !BACKUP_DIR!
    goto :fail
)

REM --- Write summary log ---
(
    echo ActoEngine Patch Apply Log
    echo ==========================
    echo Patch file : !ZIP_FILE!
    echo Target     : !TARGET_PATH!
    echo Backup     : !BACKUP_DIR!
    echo Hash       : !EXPECTED_HASH!
    echo Result     : SUCCESS
) > "%~dp0apply_patch.log"

echo.
echo ========================================
echo   Patch applied successfully.
echo ========================================
echo   Files extracted to : !TARGET_PATH!
echo   Backup created at  : !BACKUP_DIR!
echo.
echo   Next step: Open the SQL scripts from the patch
echo   in SSMS and run them against your database.
echo.
pause
endlocal
exit /b 0

:fail
echo.
echo ========================================
echo   Patch application FAILED.
echo ========================================
echo   Check the error message above and try again.
echo   If the backup folder was already created, your
echo   original files are safe there.
echo.
pause
endlocal
exit /b 1
""";
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string EscapeBatVariable(string value)
    {
        return value
            .Replace("%", "%%", StringComparison.Ordinal)
            .Replace("!", "^^!", StringComparison.Ordinal);
    }
}
