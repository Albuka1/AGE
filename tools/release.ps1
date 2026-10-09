<#
.SYNOPSIS
Cuts a release: bumps the version of the build, closes the section of the changelog, verifies the result and tags it.

.DESCRIPTION
A release of this repository is one commit that says what the version is plus the changelog section that says what changed,
and the tag that GitHub names the release by. The Release workflow then checks that the tag and the version agree before it
attaches anything, so the two files this script writes are what that check reads:

  * Directory.Build.props carries <Version>, which every assembly of the engine reports.
  * CHANGELOG.md carries the section of the version, which is what a person reads.

The script refuses to run when the tag exists, when the working tree holds changes it did not make, or when the build or the
tests fail: a release commit that mixes in work in progress is one nobody can call a release. Use -DryRun to see what it would
write without touching a file or the repository.

.PARAMETER Version
The version to release, such as 0.3.0 or 0.3.0-alpha.1. The tag is this with a v in front.

.PARAMETER DryRun
Prints what the script would write and change, and stops before it writes, builds, commits or tags anything.

.EXAMPLE
pwsh tools/release.ps1 -Version 0.3.0 -DryRun

.EXAMPLE
pwsh tools/release.ps1 -Version 0.3.0
git push origin main --follow-tags
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string] $Text)

    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Read-Text {
    param([string] $Path)

    # Read and write through the framework rather than through Set-Content, so a file keeps its own line endings and gains no
    # byte order mark: the two files this script writes are compared by a build and by a person.
    return [System.IO.File]::ReadAllText($Path)
}

function Write-Text {
    param([string] $Path, [string] $Text)

    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$') {
        throw "A version is three numbers with an optional suffix, such as 0.3.0 or 0.3.0-alpha.1, and '$Version' is not one."
    }

    if (-not (Test-Path 'Directory.Build.props') -or -not (Test-Path 'CHANGELOG.md')) {
        throw "The repository root is not '$root', and the script finds it by the folder it lives in."
    }

    $tag = "v$Version"
    $existing = git tag --list $tag

    if ($existing) {
        throw "The tag $tag already exists, and a version is released once."
    }

    $dirty = git status --porcelain

    if ($dirty) {
        throw "The working tree holds changes that are not committed, and a release commit holds the version and the changelog only. Commit or stash them first:`n$dirty"
    }

    $branch = git rev-parse --abbrev-ref HEAD
    $date = (Get-Date).ToString('yyyy-MM-dd')

    $props = Read-Text 'Directory.Build.props'
    $bumped = [regex]::Replace($props, '<Version>[^<]+</Version>', "<Version>$Version</Version>")

    if ($bumped -eq $props) {
        throw 'Directory.Build.props holds no <Version>...</Version>, so there is nothing to bump.'
    }

    $changelog = Read-Text 'CHANGELOG.md'
    $newline = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $closed = [regex]::Replace(
        $changelog,
        ('^## \[Unreleased\]' + [regex]::Escape($newline)),
        ("## [Unreleased]$newline$newline## [$Version] - $date$newline"),
        [System.Text.RegularExpressions.RegexOptions]::Multiline)

    if ($closed -eq $changelog) {
        throw "CHANGELOG.md holds no '## [Unreleased]' section to close, so nothing says what this release brings."
    }

    Write-Step "Version $Version on the branch '$branch', tagged $tag, dated $date"

    if ($DryRun) {
        Write-Host "    Directory.Build.props: <Version>$Version</Version>"
        Write-Host "    CHANGELOG.md: '## [Unreleased]' followed by '## [$Version] - $date'"
        Write-Host 'Dry run: nothing was written, built, committed or tagged.'
        return
    }

    Write-Text 'Directory.Build.props' $bumped
    Write-Text 'CHANGELOG.md' $closed

    try {
        Write-Step 'Building and testing what is about to be released'
        dotnet build Age.slnx -c Release

        if ($LASTEXITCODE -ne 0) {
            throw 'The build of the release failed.'
        }

        dotnet test --project Age.Tests/Age.Tests.csproj --no-build -c Release

        if ($LASTEXITCODE -ne 0) {
            throw 'The tests of the release failed.'
        }
    }
    catch {
        # The two files are what the build and the tests just read, so a release that does not build leaves nothing behind.
        git checkout -- Directory.Build.props CHANGELOG.md
        throw
    }

    Write-Step "Committing 'release: $Version' and tagging $tag"
    git add Directory.Build.props CHANGELOG.md
    git commit -m "release: $Version"

    # An annotated tag, because that is what a release is: it carries who made it and when, and it is what a push with
    # --follow-tags sends, which is the push this script tells a person to make.
    git tag -a $tag -m "release: $Version"

    Write-Host ''
    Write-Host "The release is committed and tagged. Push it, and the Release workflow checks that the tag and the version agree:" -ForegroundColor Green
    Write-Host "    git push origin $branch --follow-tags"
}
finally {
    Pop-Location
}