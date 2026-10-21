$deployPath = Join-Path $PSScriptRoot '..\deploy'
$alertDefinitions = Join-Path $deployPath 'OperationsAlert.ps1'
$monitorScript = Join-Path $deployPath 'Monitor-RecoveryDrill.ps1'
$taskDefinitions = Join-Path $deployPath 'RecoveryDrillTaskDefinitions.ps1'
. $alertDefinitions
. $taskDefinitions

Describe 'Send-OperationsAlert' {
    It 'does not create a mail client when SMTP is not configured' {
        { Send-OperationsAlert -Subject 'Test' -Body 'Test' } | Should Not Throw
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

        { & $monitorScript -TaskName 'Test recovery drill' } | Should Throw
    }

    It 'succeeds for a recent successful recovery drill' {
        Mock Get-ScheduledTask { [pscustomobject]@{} }
        Mock Get-ScheduledTaskInfo {
            [pscustomobject]@{
                LastRunTime = [DateTime]::Now.AddDays(-1)
                LastTaskResult = 0
            }
        }

        { & $monitorScript -TaskName 'Test recovery drill' } | Should Not Throw
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

        $registeredTaskNames | Should Be @('Test recovery drill', 'Test recovery monitor')
    }
}
