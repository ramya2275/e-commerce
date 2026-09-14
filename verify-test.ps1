# Launch microservices in background
$p = Start-Process dotnet -ArgumentList "run --project src/Services/ProductApi/ProductApi.csproj --no-launch-profile --urls http://localhost:5101" -PassThru
$o = Start-Process dotnet -ArgumentList "run --project src/Services/OrderApi/OrderApi.csproj --no-launch-profile --urls http://localhost:5102" -PassThru
$g = Start-Process dotnet -ArgumentList "run --project src/Gateways/ApiGateway/ApiGateway.csproj --no-launch-profile --urls http://localhost:5000" -PassThru

Write-Host "Services started (PIDs: $($p.Id), $($o.Id), $($g.Id)). Waiting for ports to open..."
Start-Sleep -Seconds 7

try {
    # Run tests using test-services.ps1 logic directly
    & "$PSScriptRoot\test-services.ps1" -GatewayUrl "http://localhost:5000"
}
finally {
    Write-Host "Cleaning up test processes..."
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $o.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $g.Id -Force -ErrorAction SilentlyContinue
}
