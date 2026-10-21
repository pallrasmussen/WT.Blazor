BeforeAll {
    $deployPath = Join-Path $PSScriptRoot '..\deploy'
    $alertDefinitions = Join-Path $deployPath 'OperationsAlert.ps1'
    $monitorScript = Join-Path $deployPath 'Monitor-RecoveryDrill.ps1'
    $taskDefinitions = Join-Path $deployPath 'RecoveryDrillTaskDefinitions.ps1'
    . $alertDefinitions
    . $taskDefinitions

    function Assert-DoesNotThrow([scriptblock]$Action) {
        try {
            & $Action
        }
        catch {
            throw "Expected the action not to throw, but it failed with: $($_.Exception.Message)"
        }
    }

    function Assert-Throws([scriptblock]$Action) {
        try {
            & $Action
        }
        catch {
            return
        }
        throw 'Expected the action to throw, but it completed successfully.'
    }

    function Assert-SequenceEqual([string[]]$Actual, [string[]]$Expected) {
        if ($Actual.Count -ne $Expected.Count -or ($Actual -join "`0") -ne ($Expected -join "`0")) {
            throw "Expected '$($Expected -join ', ')', but received '$($Actual -join ', ')'."
        }
    }
}

Describe 'Send-OperationsAlert' {
    It 'does not create a mail client when SMTP is not configured' {
        Assert-DoesNotThrow { Send-OperationsAlert -Subject 'Test' -Body 'Test' }
    }
}

Describe 'Monitor-RecoveryDrill' {
    It 'fails when the scheduled recovery drill has never run' {
        Mock Get-ScheduledTask { [pscustomobject]@{} }
        Mock Get-ScheduledTaskInfo {
            [pscustomobject]@{
                LastRunTime = [DateTime]::MinValue
                LastTaskResult = 0
            }
        }

        Assert-Throws { & $monitorScript -TaskName 'Test recovery drill' }
    }

    It 'succeeds for a recent successful recovery drill' {
        Mock Get-ScheduledTask { [pscustomobject]@{} }
        Mock Get-ScheduledTaskInfo {
            [pscustomobject]@{
                LastRunTime = [DateTime]::Now.AddDays(-1)
                LastTaskResult = 0
            }
        }

        Assert-DoesNotThrow { & $monitorScript -TaskName 'Test recovery drill' }
    }
}

Describe 'Install-RecoveryDrillTask' {
    It 'registers the drill and its monitor with the configured names' {
        $registeredTaskNames = [System.Collections.Generic.List[string]]::new()
        Mock Test-Path { $true }
        Mock New-ScheduledTaskPrincipal { [pscustomobject]@{} }
        Mock New-ScheduledTaskSettingsSet { [pscustomobject]@{} }
        Mock New-ScheduledTaskAction { [pscustomobject]@{} }
        Mock New-ScheduledTaskTrigger { [pscustomobject]@{} }
        $registerTask = {
            param($parameters)
            $registeredTaskNames.Add($parameters.TaskName)
        }

        Install-RecoveryDrillScheduledTasks -TaskName 'Test recovery drill' -MonitorTaskName 'Test recovery monitor' -OperationsPath 'C:\operations\WT.Blazor.Operations.dll' -RegisterTask $registerTask

        Assert-SequenceEqual $registeredTaskNames @('Test recovery drill', 'Test recovery monitor')
    }
}
