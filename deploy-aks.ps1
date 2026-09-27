<#
.SYNOPSIS
    Automated build, push, and deployment script for Azure Kubernetes Service (AKS).
.DESCRIPTION
    Builds Docker containers for all microservices, pushes them to Azure Container Registry (ACR),
    and deploys all manifests to the AKS cluster using Kustomize.
#>

[CmdletBinding()]
param(
    [string]$ResourceGroup = "Commerce-Dev-RG",
    [string]$ClusterName = "Commerce-AKS-Cluster",
    [string]$AcrName = "commerceregistry12345",
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

$RegistryServer = "$AcrName.azurecr.io"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " 🚀 Azure Kubernetes Service (AKS) Automated Deployment     " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Resource Group : $ResourceGroup" -ForegroundColor Yellow
Write-Host "AKS Cluster    : $ClusterName" -ForegroundColor Yellow
Write-Host "ACR Registry   : $RegistryServer" -ForegroundColor Yellow
Write-Host "------------------------------------------------------------"

# 1. Ensure ACR Authentication
Write-Host "`n[1/5] Authenticating Docker with Azure Container Registry ($AcrName)..." -ForegroundColor Green
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) {
    throw "Failed to authenticate with Azure Container Registry."
}

# 2. Build and Tag Images
$Services = @(
    @{ Name = "product-api"; Dockerfile = "src/Services/ProductApi/Dockerfile" },
    @{ Name = "order-api";   Dockerfile = "src/Services/OrderApi/Dockerfile" },
    @{ Name = "coupon-api";  Dockerfile = "src/Services/CouponApi/Dockerfile" },
    @{ Name = "api-gateway"; Dockerfile = "src/Gateways/ApiGateway/Dockerfile" }
)

if (-not $SkipBuild) {
    Write-Host "`n[2/5] Building Docker container images..." -ForegroundColor Green
    foreach ($svc in $Services) {
        $tag = "$RegistryServer/$($svc.Name):latest"
        Write-Host "Building $tag using $($svc.Dockerfile)..." -ForegroundColor White
        docker build -t $tag -f $svc.Dockerfile .
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed for $($svc.Name)."
        }
    }

    # 3. Push Images to ACR
    Write-Host "`n[3/5] Pushing images to Azure Container Registry..." -ForegroundColor Green
    foreach ($svc in $Services) {
        $tag = "$RegistryServer/$($svc.Name):latest"
        Write-Host "Pushing $tag..." -ForegroundColor White
        docker push $tag
        if ($LASTEXITCODE -ne 0) {
            throw "Docker push failed for $tag."
        }
    }
} else {
    Write-Host "`n[2/5] & [3/5] Skipping image build & push (--SkipBuild was specified)." -ForegroundColor Yellow
}

# 4. Configure Kubernetes Context
Write-Host "`n[4/5] Connecting kubectl to AKS cluster ($ClusterName)..." -ForegroundColor Green
az aks get-credentials --resource-group $ResourceGroup --name $ClusterName --overwrite-existing
if ($LASTEXITCODE -ne 0) {
    throw "Failed to get credentials for AKS cluster."
}

# 5. Apply Kubernetes Manifests
Write-Host "`n[5/5] Applying Kubernetes manifests via Kustomize..." -ForegroundColor Green
kubectl apply -k k8s/
if ($LASTEXITCODE -ne 0) {
    throw "kubectl apply failed."
}

Write-Host "`nWaiting for deployments to roll out successfully..." -ForegroundColor Green
$Deployments = @("product-service", "order-service", "coupon-service", "gateway-service")
foreach ($dep in $Deployments) {
    Write-Host "Checking rollout status for deployment/$dep..." -ForegroundColor White
    kubectl rollout status "deployment/$dep" -n microservices-demo --timeout=180s
}

Write-Host "`nWaiting for Azure Public IP assignment on gateway-service LoadBalancer..." -ForegroundColor Cyan
$ExternalIp = ""
$MaxWait = 30
$WaitCount = 0

while ([string]::IsNullOrWhiteSpace($ExternalIp) -and $WaitCount -lt $MaxWait) {
    $WaitCount++
    Start-Sleep -Seconds 5
    $ExternalIp = (kubectl get svc gateway-service -n microservices-demo -o jsonpath="{.status.loadBalancer.ingress[0].ip}")
    if ([string]::IsNullOrWhiteSpace($ExternalIp)) {
        $ExternalIp = (kubectl get svc gateway-service -n microservices-demo -o jsonpath="{.status.loadBalancer.ingress[0].hostname}")
    }
    Write-Host -NoNewline "."
}

Write-Host "`n`n============================================================" -ForegroundColor Green
Write-Host " 🎉 AKS Deployment Completed Successfully!                  " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($ExternalIp)) {
    Write-Host "Web Dashboard URL : http://$ExternalIp" -ForegroundColor Cyan
    Write-Host "Products API      : http://$ExternalIp/api/products" -ForegroundColor Cyan
    Write-Host "Orders API        : http://$ExternalIp/api/orders" -ForegroundColor Cyan
    Write-Host "Coupons API       : http://$ExternalIp/api/coupons" -ForegroundColor Cyan
    Write-Host "Health Probes     : http://$ExternalIp/health/live" -ForegroundColor Cyan
} else {
    Write-Host "Azure LoadBalancer is still provisioning the public IP." -ForegroundColor Yellow
    Write-Host "Run this command in a minute to check the assigned IP:" -ForegroundColor White
    Write-Host "  kubectl get svc gateway-service -n microservices-demo" -ForegroundColor Cyan
}

Write-Host "============================================================`n" -ForegroundColor Green
