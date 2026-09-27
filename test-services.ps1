param(
    [string]$GatewayUrl = "http://localhost:5000"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Testing C# Microservices Architecture & Frontend Dashboard" -ForegroundColor Cyan
Write-Host " Gateway URL: $GatewayUrl" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "Checking if Gateway ($GatewayUrl) is ready..." -ForegroundColor Yellow
$gatewayReady = $false
for ($i = 1; $i -le 15; $i++) {
    try {
        $check = Invoke-WebRequest -Uri "$GatewayUrl/health/live" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($check.StatusCode -eq 200) {
            $gatewayReady = $true
            Write-Host "Gateway is online and responding! Starting test suite..." -ForegroundColor Green
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $gatewayReady) {
    Write-Host "`n[ERROR] Unable to connect to Gateway at $GatewayUrl." -ForegroundColor Red
    Write-Host "Please ensure your services are running in a separate terminal using: .\run-local.ps1" -ForegroundColor Yellow
    Write-Host "(Keep that terminal window open while running tests!)`n" -ForegroundColor Yellow
    exit 1
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Uri,
        [object]$Body = $null,
        [int]$ExpectedStatus = 200,
        [string]$ContentType = "application/json"
    )

    Write-Host "`n[TEST] $Name" -ForegroundColor Yellow
    Write-Host "       $Method $Uri" -ForegroundColor DarkGray
    
    try {
        $params = @{
            Uri = $Uri
            Method = $Method
            ContentType = $ContentType
            ErrorAction = "Stop"
        }
        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 5)
        }

        if ($ContentType -like "*html*") {
            $webRes = Invoke-WebRequest -Uri $Uri -Method $Method -UseBasicParsing -ErrorAction Stop
            Write-Host "  -> SUCCESS (Status $ExpectedStatus)" -ForegroundColor Green
            Write-Host "  Content-Type: $($webRes.Headers['Content-Type'])" -ForegroundColor White
            Write-Host "  HTML Title Detected: $($webRes.Content.Contains('CloudShop'))" -ForegroundColor White
            return $webRes
        }

        $res = Invoke-RestMethod @params
        Write-Host "  -> SUCCESS (Status $ExpectedStatus)" -ForegroundColor Green
        Write-Host "  Response: " -NoNewline -ForegroundColor DarkGray
        Write-Host ($res | ConvertTo-Json -Compress) -ForegroundColor White
        return $res
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  -> EXPECTED STATUS ($statusCode)" -ForegroundColor Green
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errBody = $reader.ReadToEnd()
                Write-Host "  Response: $errBody" -ForegroundColor White
            } catch {}
        } else {
            Write-Host "  -> FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
        return $null
    }
}

# 1. Frontend Web Dashboard (HTML)
Test-Endpoint -Name "Frontend Dashboard Delivery" -Uri "$GatewayUrl/" -ContentType "text/html"

# 2. Gateway API Metadata Info
Test-Endpoint -Name "Gateway Info Metadata" -Uri "$GatewayUrl/api/gateway/info"

# 3. Liveness Probes
Test-Endpoint -Name "Gateway Liveness Probe" -Uri "$GatewayUrl/health/live"

# 4. Product Catalog
$productsRes = Test-Endpoint -Name "Get All Products" -Uri "$GatewayUrl/api/products"

# 5. Search Products
Test-Endpoint -Name "Search Products ('Keyboard')" -Uri "$GatewayUrl/api/products?search=Keyboard"

# 6. Coupon Catalog (via Gateway)
Test-Endpoint -Name "Get All Coupons" -Uri "$GatewayUrl/api/coupons"

# 7. Get Coupon by Code ('WELCOME10')
Test-Endpoint -Name "Get Coupon by Code ('WELCOME10')" -Uri "$GatewayUrl/api/coupons/WELCOME10"

# 8. Validate Coupon via Gateway
$validateCouponBody = @{
    code = "WELCOME10"
    orderTotal = 150.00
}
Test-Endpoint -Name "Validate Coupon ('WELCOME10' on $150 total)" -Method "POST" -Uri "$GatewayUrl/api/coupons/validate" -Body $validateCouponBody

# 9. Place New Order with Coupon Code
$orderWithCouponBody = @{
    customerEmail = "coupon.buyer@enterprise.com"
    couponCode = "WELCOME10"
    items = @(
        @{
            productId = "22222222-2222-2222-2222-222222222222"
            quantity = 1
        }
    )
}
$createdOrderWithCoupon = Test-Endpoint -Name "Place Order with Coupon 'WELCOME10' (Applies Discount)" -Method "POST" -Uri "$GatewayUrl/api/orders" -Body $orderWithCouponBody -ExpectedStatus 201

# 10. Place New Order (Standard without Coupon)
$orderBody = @{
    customerEmail = "developer@enterprise.com"
    items = @(
        @{
            productId = "11111111-1111-1111-1111-111111111111"
            quantity = 1
        },
        @{
            productId = "33333333-3333-3333-3333-333333333333"
            quantity = 2
        }
    )
}
$createdOrder = Test-Endpoint -Name "Place Order (Decrements Product Stock)" -Method "POST" -Uri "$GatewayUrl/api/orders" -Body $orderBody -ExpectedStatus 201

# 11. List Orders
Test-Endpoint -Name "List All Orders" -Uri "$GatewayUrl/api/orders"

# 12. Test Insufficient Stock Error
$invalidOrder = @{
    customerEmail = "developer@enterprise.com"
    items = @(
        @{
            productId = "44444444-4444-4444-4444-444444444444"
            quantity = 9999
        }
    )
}
Test-Endpoint -Name "Test Insufficient Stock (Expected 400 Bad Request)" -Method "POST" -Uri "$GatewayUrl/api/orders" -Body $invalidOrder -ExpectedStatus 400

# 13. Test Expired Coupon Error
$expiredCouponOrder = @{
    customerEmail = "developer@enterprise.com"
    couponCode = "EXPIRED50"
    items = @(
        @{
            productId = "22222222-2222-2222-2222-222222222222"
            quantity = 1
        }
    )
}
Test-Endpoint -Name "Test Expired Coupon (Expected 400 Bad Request)" -Method "POST" -Uri "$GatewayUrl/api/orders" -Body $expiredCouponOrder -ExpectedStatus 400

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Test run finished!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
