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
        var escapedZipFileName = EscapePowerShellSingleQuotedString(zipFileName);
        var escapedExpectedHash = EscapePowerShellSingleQuotedString(expectedHash);

        return $$"""
param(
    [string]$TargetPath = "."
)

$ErrorActionPreference = "Stop"

function Assert-PathUnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$CandidatePath,
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $resolvedCandidate = [System.IO.Path]::GetFullPath($CandidatePath)
    $resolvedRoot = [System.IO.Path]::GetFullPath($RootPath)

    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedCandidate.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $resolvedCandidate.Equals($resolvedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved path escapes target root: $CandidatePath"
    }
}

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$zipPath = Join-Path $scriptRoot '{{escapedZipFileName}}'
$expectedHash = '{{escapedExpectedHash}}'

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Patch ZIP not found next to the script: $zipPath"
}

$resolvedTargetPath = [System.IO.Path]::GetFullPath($TargetPath)
if (-not (Test-Path -LiteralPath $resolvedTargetPath)) {
    New-Item -ItemType Directory -Path $resolvedTargetPath -Force | Out-Null
}

$actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "Patch ZIP hash mismatch. Expected $expectedHash but found $actualHash."
}

$backupRoot = Join-Path $resolvedTargetPath ("backup_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
Write-Host "Creating backup at: $backupRoot"
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)

try {
    foreach ($entry in $zipArchive.Entries) {
        if ([string]::IsNullOrWhiteSpace($entry.Name)) {
            continue
        }

        $entryPath = $entry.FullName.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ($entryPath.Contains(":") -or
            [System.IO.Path]::IsPathRooted($entryPath) -or
            $entryPath.Contains(".." + [System.IO.Path]::DirectorySeparatorChar) -or
            $entryPath.EndsWith("..")) {
            throw "Unsafe archive entry detected: $($entry.FullName)"
        }

        $targetFilePath = [System.IO.Path]::GetFullPath((Join-Path $resolvedTargetPath $entryPath))
        Assert-PathUnderRoot -CandidatePath $targetFilePath -RootPath $resolvedTargetPath

        if (Test-Path -LiteralPath $targetFilePath -PathType Leaf) {
            $backupFilePath = [System.IO.Path]::GetFullPath((Join-Path $backupRoot $entryPath))
            Assert-PathUnderRoot -CandidatePath $backupFilePath -RootPath $backupRoot

            $backupDirectory = Split-Path -Parent $backupFilePath
            if (-not [string]::IsNullOrWhiteSpace($backupDirectory)) {
                New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
            }

            Write-Host "Backing up: $($entry.FullName)"
            Copy-Item -LiteralPath $targetFilePath -Destination $backupFilePath -Force
        }
    }
}
finally {
    $zipArchive.Dispose()
}

Write-Host "Extracting ZIP to: $resolvedTargetPath"
Expand-Archive -LiteralPath $zipPath -DestinationPath $resolvedTargetPath -Force

Write-Host "Patch applied successfully."
""";
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
