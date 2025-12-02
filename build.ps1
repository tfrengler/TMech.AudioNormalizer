if ($args.Count -eq 0)
{
    Write-Host -f Red "No args passed"
    exit
}

$Config = $args[0];
$RunTime = $args[1];

Write-Host -f Yellow "Publishing ($Config | $RunTime)..."
$BuildOutput = dotnet publish -c $Config --runtime $RunTime

if ($BuildOutput -match "Publish FAILED")
{
    Write-Host -f Red "Publish failed! Aborting"
    exit
}

Write-Host -f Green "Publish done, no errors"