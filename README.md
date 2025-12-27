### Distributed Rate Limiting 

<img width="1383" height="684" alt="image" src="https://github.com/user-attachments/assets/de072dbc-88ef-4130-9b4b-95ed89e5090e" />


A Docker-based distributed rate limiting system built with ASP.NET Core, Redis, etcd, and NGINX.

## Architecture Overview

- **Load Balancer (NGINX)**: Front door that forwards requests to the API.
- **RateLimiting API**: Applies distributed rate limiting middleware and serves API endpoints.
- **Rules Service**: Polls etcd for policy changes and writes them to Redis.
- **Redis**: Stores rate-limit configuration and runtime counters; also acts as a notification bus.
- **etcd**: Source of truth for rate-limit policies.

## Services and Ports

| Service              | Purpose             | Port (host -> container)  |
| -------------------- | ------------------- | ------------------------- |
| load-balancer        | NGINX reverse proxy | `8080:80`                 |
| ratelimiting-service | API                 | `8079-8081:80` (replicas) |
| rules-daemon-service | Policy sync worker  | `8082:80`                 |
| redis                | Cache / counters    | `6379:6379`               |
| etcd                 | Policy store        | `2379:2379`, `2380:2380`  |

## Prerequisites

- Docker + Docker Compose
- Optional: .NET 8 SDK (for local runs outside Docker)

## Quick Start (Docker)

```bash
docker compose up -d --build
```

Check service status:

```bash
docker compose ps
```

## How Policies Flow

1. Policies are stored in etcd at `Etcd:PoliciesKey` (default `/v2/keys/rl/policies.json`).
2. The Rules Service polls etcd and writes the JSON to Redis under `RulesSync:RedisKey`.
3. The API reads the config from Redis and applies distributed rate limiting.

## Key Configuration

### RateLimiting API

`RateLimiting/RateLimitingApi/appsettings.json`

- `RateLimitingConfig.RedisKey`: Redis key containing options.
- `RateLimiting.ClusterNodeCount`: number of logical nodes.
- `RateLimiting.Algorithms`: list of algorithms (`SlidingWindow`, `FixedWindow`, `TokenBucket`).

### Rules Service

`RulesService/appsettings.json`

- `Etcd.BaseUrl`: etcd endpoint.
- `Etcd.PoliciesKey`: key path to read the policy JSON.
- `RulesSync.RedisKey`: Redis key where policies are written.
- `RulesSync.NotificationChannel`: Pub/sub channel for updates.

## Example Policy Value in etcd

`/v2/keys/rl/policies.json` should contain a JSON payload matching the API options schema, e.g.:

```json
{
  "ClusterNodeCount": 2,
  "ClientIdHeader": "X-Client-Id",
  "DefaultWindowSeconds": 60,
  "DefaultMaxRequests": 30,
  "Algorithms": [
    { "Name": "sliding-window", "Type": "SlidingWindow", "MaxRequests": 15, "WindowSeconds": 60, "Enabled": true },
    { "Name": "fixed-window", "Type": "FixedWindow", "MaxRequests": 10, "WindowSeconds": 30, "Enabled": true },
    { "Name": "token-bucket", "Type": "TokenBucket", "Capacity": 20, "RefillRatePerSecond": 5, "Enabled": true }
  ]
}
```

## Test the Rate Limiter

The API exposes a sample endpoint at:

```
GET http://localhost:8080/WeatherForecast
```

### Single Client Example

```bash
curl -i http://localhost:8080/WeatherForecast \
  -H "X-Client-Id: demo-client"
```

### Burst to Trigger 429

```bash
for i in {1..12}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -H "X-Client-Id: demo-client" \
    http://localhost:8080/WeatherForecast
done
```

When throttled, you will receive `429` with headers like:

- `Retry-After`
- `X-RateLimit-Node`
- `X-RateLimit-Algorithm`

## Troubleshooting

### NGINX fails to start with resolver error

If you see:

```
resolving names at run time requires upstream "apipool" to be in shared memory
```

Ensure `LoadBalancer/nginx.conf` includes:

```
upstream apipool {
  zone apipool 64k;
  least_conn;
  server ratelimiting-service:80 resolve;
}
```

### Rules Service fails on etcd/redis hostnames

When running in Docker, use `etcd` and `redis` hostnames (not `localhost`) inside containers.

### Rules Service exits on etcd failure

The rules worker retries etcd GETs with exponential backoff; check logs with:

```bash
docker compose logs -f rules-daemon-service
```

### Redis connection errors

The Redis client is configured to retry (`AbortOnConnectFail = false`). If it still fails, check Redis health:

```bash
docker compose logs -f redis
```

## Running Without Docker (Optional)

If you run the services locally (not in Docker), set:

- `ConnectionStrings:Redis` to `localhost:6379`
- `Etcd:BaseUrl` to `http://localhost:2379`

and start Redis/etcd locally.

## Project Structure

- `LoadBalancer/` - NGINX configuration
- `RateLimiting/` - ASP.NET Core API + rate-limiting middleware
- `RulesService/` - Rules sync worker
- `docker-compose.yaml` - Multi-service runtime

## Testing with Real-World APIs

If you want to test the rate limiter against real APIs rather than the sample `WeatherForecast` endpoint,
use one of the options below.

### Option A: Add a passthrough endpoint

Add a controller in `RateLimiting/RateLimitingApi` that proxies requests to a real API (for example,
`https://httpbin.org`). The rate limiter will still run because it is middleware on every request.

Example request after adding a `/proxy/get` endpoint:

```bash
curl -i http://localhost:8080/proxy/get -H "X-Client-Id: demo-client"
```

### Option B: Point NGINX upstream to a real API (testing only)

Edit `LoadBalancer/nginx.conf` and replace the upstream `server` with a real host:

```
upstream apipool {
  zone apipool 64k;
  least_conn;
  server httpbin.org:80 resolve;
}
```

Then restart the load balancer:

```bash
docker compose restart load-balancer
```

### Testing Checklist

1. **Start services**
   ```bash
   docker compose up -d --build
   ```
2. **Send repeated requests**
   ```bash
   for i in {1..12}; do
     curl -s -o /dev/null -w "%{http_code}\n" \
       -H "X-Client-Id: demo-client" \
       http://localhost:8080/WeatherForecast
   done
   ```
3. **Concurrent requests **
    ```bash 
   for client in alice bob; do
    echo "Client: $client"
    for i in {1..6}; do
    curl -s -o /dev/null -w "%{http_code}\n" \
      -H "X-Client-Id: $client" \
      http://localhost:8080/WeatherForecast
    done
    done
    ```

1. **Confirm 429s and headers**
   - Expect `429` after limits are exceeded.
   - Check `Retry-After`, `X-RateLimit-Node`, `X-RateLimit-Algorithm`.

## License

This project is provided as-is for educational and demo purposes.