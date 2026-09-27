# Run all 3 microservices locally in separate background processes
Write-Host "Starting ProductApi on http://localhost:5101 ..." -ForegroundColor Green
$productProc = Start-Process dotnet -ArgumentList "run --project src/Services/ProductApi/ProductApi.csproj --no-launch-profile --urls http://localhost:5101" -PassThru

Write-Host "Starting OrderApi on http://localhost:5102 ..." -ForegroundColor Green
$orderProc = Start-Process dotnet -ArgumentList "run --project src/Services/OrderApi/OrderApi.csproj --no-launch-profile --urls http://localhost:5102" -PassThru

Write-Host "Starting CouponApi on http://localhost:5103 ..." -ForegroundColor Green
$couponProc = Start-Process dotnet -ArgumentList "run --project src/Services/CouponApi/CouponApi.csproj --no-launch-profile --urls http://localhost:5103" -PassThru

Write-Host "Starting ApiGateway on http://localhost:5000 ..." -ForegroundColor Green
$gatewayProc = Start-Process dotnet -ArgumentList "run --project src/Gateways/ApiGateway/ApiGateway.csproj --no-launch-profile --urls http://localhost:5000" -PassThru

Write-Host "`nStarting services..." -ForegroundColor Cyan
Write-Host "  ProductApi: http://localhost:5101/api/products" -ForegroundColor Gray
Write-Host "  OrderApi:   http://localhost:5102/api/orders" -ForegroundColor Gray
Write-Host "  CouponApi:  http://localhost:5103/api/coupons" -ForegroundColor Gray
Write-Host "  ApiGateway: http://localhost:5000" -ForegroundColor Gray

Write-Host "`nWaiting 5 seconds for services to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5
Write-Host "Services are now running!" -ForegroundColor Green
Write-Host "Open a SECOND terminal window to test: .\test-services.ps1" -ForegroundColor Cyan
Write-Host "(Press Enter or CTRL+C in THIS window whenever you want to stop all services)`n" -ForegroundColor Yellow

try {
    Read-Host
} finally {
    Write-Host "Stopping services..." -ForegroundColor Red
    Stop-Process -Id $productProc.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $orderProc.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $couponProc.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $gatewayProc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Services stopped." -ForegroundColor Green
}
