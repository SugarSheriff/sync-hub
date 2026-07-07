<#
.SYNOPSIS
    Scheduled health check for the SAP B1 <-> Wrike sync queue.

.DESCRIPTION
    Runs on a schedule (e.g. every 5 minutes via Task Scheduler / cron).
    Checks the sync queue depth, flags any record that's been stuck longer
    than the configured threshold, and posts a summary to a Slack webhook
    so the on-call engineer isn't the one who finds out from a customer.

.NOTES
    Author: Liam Hulsey
    This is a demo script — swap Get-QueueSnapshot's mock data for a real
    call to your queue store (SQL Server table, Redis, etc).
#>

param(
    [int]$StuckThresholdMinutes = 15,
    [string]$SlackWebhookUrl = $env:SYNC_HUB_SLACK_WEBHOOK
)

function Get-QueueSnapshot {
    # In production this would query the actual sync queue table.
    # Mocked here so the script is runnable standalone as a demo.
    [PSCustomObject]@{
        TotalRecords = 42
        Records = @(
            [PSCustomObject]@{ OrderId = 'SO-10231'; EnqueuedAt = (Get-Date).AddMinutes(-2) },
            [PSCustomObject]@{ OrderId = 'SO-10214'; EnqueuedAt = (Get-Date).AddMinutes(-22) },
            [PSCustomObject]@{ OrderId = 'SO-10198'; EnqueuedAt = (Get-Date).AddMinutes(-4) }
        )
    }
}

function Get-StuckRecords {
    param($Snapshot, $ThresholdMinutes)
    $cutoff = (Get-Date).AddMinutes(-$ThresholdMinutes)
    return $Snapshot.Records | Where-Object { $_.EnqueuedAt -lt $cutoff }
}

function Send-SlackSummary {
    param([string]$WebhookUrl, [string]$Message)

    if ([string]::IsNullOrWhiteSpace($WebhookUrl)) {
        Write-Host "[dry-run] Would post to Slack:`n$Message"
        return
    }

    $body = @{ text = $Message } | ConvertTo-Json
    Invoke-RestMethod -Uri $WebhookUrl -Method Post -Body $body -ContentType 'application/json'
}

$snapshot = Get-QueueSnapshot
$stuck = Get-StuckRecords -Snapshot $snapshot -ThresholdMinutes $StuckThresholdMinutes

$summary = "Sync Hub health check`nQueue depth: $($snapshot.TotalRecords)`nStuck records (>$StuckThresholdMinutes min): $($stuck.Count)"

if ($stuck.Count -gt 0) {
    $summary += "`n" + (($stuck | ForEach-Object { "  - $($_.OrderId) stuck since $($_.EnqueuedAt)" }) -join "`n")
    Write-Warning "Found $($stuck.Count) stuck record(s)."
} else {
    Write-Host "Queue healthy. No stuck records."
}

Send-SlackSummary -WebhookUrl $SlackWebhookUrl -Message $summary
