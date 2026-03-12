# AleaSim — k3s Deployment Guide

## 1. What is k3s and Why We Use It

**k3s** is a lightweight, CNCF-certified Kubernetes distribution created by Rancher Labs. It bundles the full Kubernetes API surface into a single ~70 MB binary and replaces heavy components (etcd → SQLite/embedded etcd, cloud providers → optional) to make it practical on modest hardware.

### Why k3s for AleaSim
| Concern | k3s Answer |
| :--- | :--- |
| Small VPS footprint (2–4 CPU, 4–8 GB RAM) | Single-process server; no separate etcd daemon needed |
| Operator simplicity | One `curl \| sh` install, built-in Traefik ingress controller |
| Full `kubectl` compatibility | 100% Kubernetes API — all standard manifests work unchanged |
| Low-latency spin requests | Co-located services inside the cluster eliminate network hops |
| Future scale-out | Add worker nodes with a single command; no architecture change |

---

## 2. Cluster Topology

### Nodes
```
┌─────────────────────────────────────┐
│  k3s Server (Control Plane + etcd)  │   e.g. 4 vCPU / 8 GB RAM
│  hostname: aleasim-master           │
└───────────────┬─────────────────────┘
                │  (optional workers)
     ┌──────────┴──────────┐
     │  k3s Agent Node 1   │   e.g. 2 vCPU / 4 GB RAM
     │  hostname: worker-1 │
     └─────────────────────┘
```

For a single-node development setup the server node also acts as the worker.

### Namespaces
| Namespace | Purpose |
| :--- | :--- |
| `aleasim` | All platform workloads (API, Client, Redis, MySQL) |
| `kube-system` | Traefik ingress, CoreDNS, metrics-server |
| `cert-manager` *(optional)* | Automatic TLS via Let's Encrypt |

All manifests in this repository target the `aleasim` namespace.

---

## 3. Services Deployed

| Service Name | Type | Port | Description |
| :--- | :--- | :--- | :--- |
| `aleasim-api` | ClusterIP | 8080 | .NET 8 Web API backend |
| `aleasim-client` | ClusterIP | 3000 | Next.js / React frontend |
| `redis` | ClusterIP | 6379 | Session state, Brain directives, distributed locks |
| `mysql` | ClusterIP | 3306 | Persistent financial ledger & player data |

All services are internal `ClusterIP`. External traffic is routed through the Traefik ingress controller.

---

## 4. Kubernetes Resource Types Used

| Resource | Purpose |
| :--- | :--- |
| `Deployment` | Manages replica sets for stateless workloads (API, Client, Redis) |
| `StatefulSet` | Manages MySQL (stable network identity + ordered pod management) |
| `Service (ClusterIP)` | Internal DNS-based service discovery |
| `Ingress` | Routes HTTP/HTTPS traffic from Traefik to backend services |
| `ConfigMap` | Non-secret environment variables (DB host, Redis host, CORS origins) |
| `Secret` | Sensitive values (DB password, JWT signing key, Brain API key) |
| `PersistentVolumeClaim` | MySQL data directory backed by the k3s local-path provisioner |
| `HorizontalPodAutoscaler` | Auto-scales the API pods based on CPU/memory utilization |

---

## 5. Building and Pushing Docker Images

### API Image (AleaSim.Api)
```dockerfile
# docs/docker/Dockerfile.api
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AleaSim.sln .
COPY AleaSim.Api/          AleaSim.Api/
COPY AleaSim.Domain/       AleaSim.Domain/
COPY AleaSim.Persistence/  AleaSim.Persistence/
COPY AleaSim.Shared/       AleaSim.Shared/
RUN dotnet publish AleaSim.Api/AleaSim.Api.csproj \
    -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AleaSim.Api.dll"]
```

### Client Image (AleaSim.Client)
```dockerfile
# docs/docker/Dockerfile.client
FROM node:20-alpine AS deps
WORKDIR /app
COPY AleaSim.Client/package*.json ./
RUN npm ci --omit=dev

FROM node:20-alpine AS build
WORKDIR /app
COPY AleaSim.Client/ .
COPY --from=deps /app/node_modules ./node_modules
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
COPY --from=build /app/.next/standalone .
COPY --from=build /app/public ./public
EXPOSE 3000
CMD ["node", "server.js"]
```

### Build & Push Commands
```bash
# Set your container registry
REGISTRY=ghcr.io/your-org/aleasim
TAG=$(git rev-parse --short HEAD)

# Build and push API
docker build -f docs/docker/Dockerfile.api -t $REGISTRY/api:$TAG .
docker push $REGISTRY/api:$TAG

# Build and push Client
docker build -f docs/docker/Dockerfile.client -t $REGISTRY/client:$TAG .
docker push $REGISTRY/client:$TAG

# Tag latest
docker tag $REGISTRY/api:$TAG    $REGISTRY/api:latest
docker tag $REGISTRY/client:$TAG $REGISTRY/client:latest
docker push $REGISTRY/api:latest
docker push $REGISTRY/client:latest
```

---

## 6. Applying Manifests

Manifests live under `docs/k3s/`. Apply them in dependency order:

```bash
# 1. Create namespace
kubectl apply -f docs/k3s/namespace.yaml

# 2. Secrets and ConfigMaps (must exist before Deployments reference them)
kubectl apply -f docs/k3s/configmap.yaml
kubectl apply -f docs/k3s/secrets.yaml

# 3. Storage (PVC for MySQL)
kubectl apply -f docs/k3s/mysql-pvc.yaml

# 4. Data tier
kubectl apply -f docs/k3s/mysql-statefulset.yaml
kubectl apply -f docs/k3s/redis-deployment.yaml

# 5. Application tier
kubectl apply -f docs/k3s/api-deployment.yaml
kubectl apply -f docs/k3s/client-deployment.yaml

# 6. Ingress
kubectl apply -f docs/k3s/ingress.yaml

# Or apply everything at once (Kubernetes resolves ordering internally):
kubectl apply -f docs/k3s/
```

### Example Deployment Manifest (`api-deployment.yaml`)
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: aleasim-api
  namespace: aleasim
spec:
  replicas: 2
  selector:
    matchLabels:
      app: aleasim-api
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0          # zero-downtime updates
  template:
    metadata:
      labels:
        app: aleasim-api
    spec:
      containers:
        - name: api
          image: ghcr.io/your-org/aleasim/api:latest
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: aleasim-config
            - secretRef:
                name: aleasim-secrets
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 20
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
          resources:
            requests:
              cpu: "250m"
              memory: "256Mi"
            limits:
              cpu: "1000m"
              memory: "512Mi"
```

### Example Ingress Manifest (`ingress.yaml`)
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: aleasim-ingress
  namespace: aleasim
  annotations:
    traefik.ingress.kubernetes.io/router.entrypoints: websecure
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
    - hosts:
        - aleasim.example.com
      secretName: aleasim-tls
  rules:
    - host: aleasim.example.com
      http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: aleasim-api
                port:
                  number: 8080
          - path: /
            pathType: Prefix
            backend:
              service:
                name: aleasim-client
                port:
                  number: 3000
```

---

## 7. Persistent Volume Claims (MySQL)

k3s ships with the **local-path provisioner** which dynamically creates host-path volumes.

```yaml
# docs/k3s/mysql-pvc.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: mysql-data-pvc
  namespace: aleasim
spec:
  accessModes:
    - ReadWriteOnce
  storageClassName: local-path
  resources:
    requests:
      storage: 20Gi
```

The MySQL StatefulSet mounts this PVC at `/var/lib/mysql`. Data survives pod restarts and node reboots.

> **Production note:** For multi-node clusters, replace `local-path` with a network-attached storage class (e.g., Longhorn, NFS) to allow pod rescheduling across nodes without data loss.

---

## 8. Health Checks & Liveness Probes

### API Health Endpoints
The .NET API exposes two health check endpoints via `Microsoft.Extensions.Diagnostics.HealthChecks`:

| Endpoint | Purpose | Checked by |
| :--- | :--- | :--- |
| `GET /health/live` | Is the process alive? (no DB check) | `livenessProbe` |
| `GET /health/ready` | Can it serve traffic? (DB + Redis reachable) | `readinessProbe` |

### Probe Configuration
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 15   # Grace period for .NET startup
  periodSeconds: 20
  timeoutSeconds: 5
  failureThreshold: 3        # Restart after 3 consecutive failures

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 2        # Remove from load balancer after 2 failures
```

### Redis Health Check
```yaml
livenessProbe:
  exec:
    command: ["redis-cli", "ping"]
  initialDelaySeconds: 5
  periodSeconds: 10
```

### MySQL Health Check
```yaml
livenessProbe:
  exec:
    command:
      - sh
      - -c
      - "mysqladmin ping -h localhost -u root -p$MYSQL_ROOT_PASSWORD"
  initialDelaySeconds: 30
  periodSeconds: 15
```

---

## 9. Rolling Update Strategy

All Deployments use `RollingUpdate` to guarantee zero-downtime deployments:

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1        # Spin up 1 extra pod before removing old ones
    maxUnavailable: 0  # Never reduce below the desired replica count
```

**Update flow:**
1. New pod starts → readinessProbe passes → pod added to Service endpoints.
2. One old pod removed → terminates gracefully (SIGTERM + 30s `terminationGracePeriodSeconds`).
3. Repeat until all replicas are updated.

To trigger a rolling update after pushing a new image:
```bash
kubectl set image deployment/aleasim-api \
  api=ghcr.io/your-org/aleasim/api:new-tag \
  -n aleasim

# Monitor rollout
kubectl rollout status deployment/aleasim-api -n aleasim

# Roll back if something goes wrong
kubectl rollout undo deployment/aleasim-api -n aleasim
```

---

## 10. Accessing the Cluster

### Install kubectl (if not already present)
```bash
# Linux
curl -LO "https://dl.k8s.io/release/$(curl -Ls https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl && sudo mv kubectl /usr/local/bin/
```

### Copy kubeconfig from the k3s server
```bash
# On the k3s server node
sudo cat /etc/rancher/k3s/k3s.yaml

# Copy to your workstation, replacing 127.0.0.1 with the server's public IP
scp user@aleasim-master:/etc/rancher/k3s/k3s.yaml ~/.kube/config
sed -i 's/127.0.0.1/<SERVER_PUBLIC_IP>/g' ~/.kube/config
```

### Common kubectl Commands
```bash
# List all pods in the aleasim namespace
kubectl get pods -n aleasim

# Stream API logs
kubectl logs -f deployment/aleasim-api -n aleasim

# Exec into the API container for debugging
kubectl exec -it deployment/aleasim-api -n aleasim -- /bin/sh

# Check resource usage
kubectl top pods -n aleasim

# Describe a failing pod (events section is key)
kubectl describe pod <pod-name> -n aleasim

# List all services and their cluster IPs
kubectl get svc -n aleasim

# Check ingress routing rules
kubectl get ingress -n aleasim -o yaml

# View ConfigMap values
kubectl get configmap aleasim-config -n aleasim -o yaml

# Scale the API deployment manually
kubectl scale deployment/aleasim-api --replicas=3 -n aleasim
```

---

## 11. Troubleshooting Common Issues

### Pod in CrashLoopBackOff
```bash
# Check the last crash reason
kubectl describe pod <pod-name> -n aleasim
kubectl logs <pod-name> -n aleasim --previous   # logs from previous crashed instance
```
**Common causes:**
- Missing environment variable → check `configmap.yaml` and `secrets.yaml` are applied.
- Port conflict → ensure `containerPort` matches the app's listening port.
- Failed DB connection on startup → verify MySQL pod is `Running` before API starts (use `initContainers` if needed).

### OOMKilled (Out of Memory)
```bash
kubectl describe pod <pod-name> -n aleasim | grep -A5 "Last State"
# Look for: Reason: OOMKilled
```
**Fix:** Increase `resources.limits.memory` in the Deployment manifest. For the API, start with `512Mi` and monitor with `kubectl top pods`.

### Database Connection Refused
```bash
# Test MySQL connectivity from inside the API pod
kubectl exec -it deployment/aleasim-api -n aleasim -- \
  sh -c "nc -zv mysql 3306 && echo OK"
```
**Common causes:**
- `MYSQL_HOST` ConfigMap value is wrong (use the Kubernetes Service name: `mysql`, not `localhost`).
- MySQL pod is not yet `Ready` (check readiness probe).
- Secret `MYSQL_PASSWORD` does not match the value set in the MySQL StatefulSet.

### Ingress Not Routing Traffic
```bash
kubectl get ingress -n aleasim
kubectl describe ingress aleasim-ingress -n aleasim
# Check Traefik logs
kubectl logs -n kube-system deployment/traefik
```
**Common causes:**
- DNS not pointing to the node's public IP.
- TLS secret missing (cert-manager not installed or issuer misconfigured).
- Backend service name typo in the ingress rule.

### Redis Connection Timeout
```bash
# From inside the API pod
kubectl exec -it deployment/aleasim-api -n aleasim -- \
  sh -c "redis-cli -h redis ping"
```
The `REDIS_HOST` ConfigMap should be set to `redis` (the Kubernetes Service name).

---

## 12. Security Checklist

- [ ] All secrets stored in Kubernetes `Secret` objects (base64-encoded), **never** in ConfigMaps or source code.
- [ ] MySQL and Redis Services are `ClusterIP` (not exposed externally).
- [ ] Traefik configured to redirect HTTP → HTTPS.
- [ ] TLS certificate auto-renewed by cert-manager.
- [ ] Container images run as non-root (`runAsUser: 1000` in `securityContext`).
- [ ] Network policies restrict cross-namespace traffic.
- [ ] `kubectl` access controlled via RBAC (no cluster-admin for CI/CD service accounts).
