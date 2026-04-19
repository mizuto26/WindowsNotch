param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("feat", "fix", "refactor", "docs", "style", "test", "build", "ci", "chore", "perf", "revert")]
    [string]$Type,

    [Parameter(Mandatory = $true)]
    [string]$IssueNumber,

    [Parameter(Mandatory = $true)]
    [string]$Summary
)

$ErrorActionPreference = "Stop"

if ($IssueNumber -notmatch '^\d+$') {
    throw "IssueNumber must be numeric."
}

$normalizedSummary = $Summary.ToLowerInvariant()
$normalizedSummary = $normalizedSummary -replace '[^a-z0-9]+', '-'
$normalizedSummary = $normalizedSummary -replace '^-+', ''
$normalizedSummary = $normalizedSummary -replace '-+$', ''
$normalizedSummary = $normalizedSummary -replace '-{2,}', '-'

if ([string]::IsNullOrWhiteSpace($normalizedSummary)) {
    throw "Summary must contain at least one letter or number."
}

$branchName = "$Type/$IssueNumber-$normalizedSummary"

Write-Host "Creating branch: $branchName"
git checkout -b $branchName
