# C# Microservices Architecture (.NET 10) with REST APIs & Kubernetes

An enterprise-grade, end-to-end sample microservices project built in **C# (.NET 10)** showcasing:
- **Interactive Web Frontend Dashboard**: Built into the API Gateway (`http://localhost:5000`) with glassmorphic dark-mode UI, live product search, interactive shopping cart, order tracking with stock rollback, and cluster health observability.
- **RESTful API Design & Best Practices**: Resource-based URLs, standard HTTP status codes (`200`, `201`, `400`, `404`), OpenAPI specifications, and standardized API response envelopes.
- **Microservices Architecture**: Service decomposition, independent deployability, thread-safe repositories, inter-service HTTP communication via resilient `HttpClient`, and compensating transactions.
- **API Gateway Pattern**: Microsoft **YARP (Yet Another Reverse Proxy)** as an intelligent API Gateway routing traffic, managing CORS, and aggregating endpoints.
- **Containerization**: Multi-stage, production-ready `Dockerfiles` utilizing minimal ASP.NET runtime images.
- **Kubernetes (K8s) Orchestration**: Production-grade YAML manifests covering `Namespace`, `ConfigMap`, `Deployments`, `Services` (ClusterIP & NodePort), `Liveness` & `Readiness` Probes, `Ingress`, and `HorizontalPodAutoscaler` (HPA).

---

## 🏛 Architecture Overview

```
                      [ External Client / Web Browser ]
                                     |
                                     v HTTP (Port 5000)
                    +----------------------------------+
                    |       API Gateway (YARP)         |
                    |    + Web Frontend Dashboard      |
                    +----------------------------------+
                                     |
                   +-----------------+-----------------+
                   | /api/products/*                   | /api/orders/*
                   v                                   v
        +---------------------+             +---------------------+
        |     ProductApi      | <========== |      OrderApi       |
        |  (Catalog & Stock)  |  HTTP Call  |  (Order Processing) |
        |     [Port 8080]     |             |     [Port 8081]     |
        +---------------------+             +---------------------+
             /health/live                        /health/live
             /health/ready                       /health/ready
```

### Components Breakdown

| Service | Port (Local) | Port (Container/K8s) | Role & Responsibilities |
|---|---|---|---|
| **`Web Frontend`** | `5000` | `5000` | Glassmorphic single page dashboard served by `ApiGateway` on `/` for shopping, orders, and cluster health. |
| **`SharedContracts`** | N/A | N/A | Shared DTOs (`ProductDto`, `OrderDto`), request contracts, and standard `ApiResponse<T>` envelope. |
| **`ProductApi`** | `5101` | `8080` | Catalog microservice managing products, prices, and stock inventory. Exposes Kubernetes health probes. |
| **`OrderApi`** | `5102` | `8081` | Order processing microservice. Communicates with `ProductApi` to validate stock, deducts inventory, and records orders. |
| **`ApiGateway`** | `5000` | `5000` | Reverse proxy powered by **YARP**. Routes `/api/products/*` and `/api/orders/*` and serves the web frontend. |

---

## 📡 REST API Reference

All responses follow a consistent `ApiResponse<T>` envelope:
```json
{
  "success": true,
  "message": "Product created successfully",
  "data": { ... },
  "errors": null
}
```

### Product Service Endpoints
| HTTP Method | Route | Description |
|---|---|---|
| `GET` | `/api/products` | Retrieve all products (supports `?search=` filter) |
| `GET` | `/api/products/{id}` | Retrieve a specific product by GUID |
| `POST` | `/api/products` | Add a new product to catalog (`201 Created`) |
| `PUT` | `/api/products/{id}/stock` | Update stock quantity (positive to add, negative to deduct) |
| `GET` | `/health/live` | Kubernetes Liveness Probe |
| `GET` | `/health/ready` | Kubernetes Readiness Probe |

### Order Service Endpoints
| HTTP Method | Route | Description |
|---|---|---|
| `GET` | `/api/orders` | Retrieve all placed orders (supports `?customerEmail=` filter) |
| `GET` | `/api/orders/{id}` | Retrieve specific order by GUID |
| `POST` | `/api/orders` | Create an order. Validates with `ProductApi`, deducts stock, and commits order |
| `POST` | `/api/orders/{id}/cancel` | Cancel order and restores reserved stock back to `ProductApi` |
| `GET` | `/health/live` | Kubernetes Liveness Probe |
| `GET` | `/health/ready` | Kubernetes Readiness Probe (validates downstream `ProductApi` reachability) |

---

## 🚀 Running the Project

### Option 1: Run Directly via .NET CLI (Local Development)

You can run the automated script to launch all 3 services concurrently:
```powershell
.\run-local.ps1
```

Or run each project in a separate terminal:
```powershell
# Terminal 1 - Product Service (:5101)
dotnet run --project src/Services/ProductApi/ProductApi.csproj

# Terminal 2 - Order Service (:5102)
dotnet run --project src/Services/OrderApi/OrderApi.csproj

# Terminal 3 - API Gateway (:5000)
dotnet run --project src/Gateways/ApiGateway/ApiGateway.csproj
```

Once running, access the Gateway at **`http://localhost:5000`**.

---

### Option 2: Run with Docker Compose

Build and launch all services in an isolated bridge network with health checks:
```bash
docker compose up --build
```

To stop:
```bash
docker compose down
```

Services will be accessible on:
- API Gateway: `http://localhost:5000`
- Product Service: `http://localhost:8080`
- Order Service: `http://localhost:8081`

---

### Option 3: Deploy to Kubernetes (K8s)

The `k8s/` directory contains complete Kubernetes manifests.

#### 1. Build and Tag Docker Images
```bash
docker build -t microservices/product-api:latest -f src/Services/ProductApi/Dockerfile .
docker build -t microservices/order-api:latest -f src/Services/OrderApi/Dockerfile .
docker build -t microservices/api-gateway:latest -f src/Gateways/ApiGateway/Dockerfile .
```
*(If using Minikube, run `eval $(minikube docker-env)` before building so images are available in Minikube's Docker daemon).*

#### 2. Apply Manifests
You can deploy all manifests in one command via Kustomize:
```bash
kubectl apply -k k8s/
```
Or apply individually:
```bash
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/01-configmap.yaml
kubectl apply -f k8s/02-product-service.yaml
kubectl apply -f k8s/03-order-service.yaml
kubectl apply -f k8s/04-gateway-service.yaml
kubectl apply -f k8s/05-ingress.yaml
kubectl apply -f k8s/06-hpa.yaml
```

#### 3. Inspect Deployed Resources
```bash
# Check running pods
kubectl get pods -n microservices-demo

# Check services and NodePort
kubectl get svc -n microservices-demo

# Check Horizontal Pod Autoscalers
kubectl get hpa -n microservices-demo
```

#### 4. Access the Gateway in Kubernetes
- **NodePort**: `http://<Node-IP>:30500` (on Minikube: `minikube service gateway-service -n microservices-demo --url`)
- **Ingress**: Configured for `http://<Ingress-Host>/api/products` and `http://<Ingress-Host>/api/orders`

---

## 🧪 Testing & Verification

### 1. Automated PowerShell Test Script
While the services are running, run:
```powershell
.\test-services.ps1
```
This script automatically performs:
1. Gateway health check and root info check
2. Catalog listing and search filtering
3. Order creation with product validation and stock deduction
4. Insufficient stock failure handling (expecting HTTP 400 Bad Request)

### 2. VS Code / Visual Studio REST Client
Open [test-api.http](test-api.http) and click **Send Request** on any endpoint to test interactively.

---

## ☸️ Key Kubernetes Patterns Demonstrated

1. **Service Discovery & Internal DNS**:
   The `OrderApi` talks to `ProductApi` using cluster-internal DNS:
   `http://product-service.microservices-demo.svc.cluster.local:8080`.
2. **Liveness & Readiness Probes**:
   - `livenessProbe`: Periodically polls `/health/live`. If the app crashes or enters a deadlock, kubelet restarts the container.
   - `readinessProbe`: Periodically polls `/health/ready`. Prevents traffic from reaching the pod until its initialization and downstream dependencies are healthy.
3. **Resource Limits & Requests**:
   Every container defines `cpu` and `memory` requests and limits to guarantee predictable QoS and prevent noisy-neighbor issues.
4. **Horizontal Pod Autoscaling (HPA)**:
   Automatically scales replicas from `2` up to `5` when average CPU utilization exceeds 70%.
5. **ConfigMaps**:
   Externalizes environment variables and service URLs without modifying container images.
