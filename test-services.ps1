param(
    [string]$GatewayUrl = "http://localhost:5000"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Testing C# Microservices Architecture & Frontend Dashboard" -ForegroundColor Cyan
Write-Host " Gateway URL: $GatewayUrl" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

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

# 6. Place New Order
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

# 7. List Orders
Test-Endpoint -Name "List All Orders" -Uri "$GatewayUrl/api/orders"

# 8. Test Insufficient Stock Error
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

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Test run finished!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
