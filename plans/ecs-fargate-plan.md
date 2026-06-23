# Bitly Clone Infrastructure Plan: ECS Fargate

## What This Plan Is

This version is built like a more traditional production web app. The .NET app runs inside containers on AWS ECS Fargate. AWS manages the servers, but the containers stay running even when nobody is using the app.

This is better for learning production-style container infrastructure, but it is usually not the best choice for strict free-tier usage.

## Infrastructure Overview

| Part | Service | Beginner explanation |
| --- | --- | --- |
| Container image storage | Amazon ECR | Stores Docker images for the .NET app and worker. ECS pulls images from here. |
| Web compute | ECS Fargate service | Runs the ASP.NET Core app as one or more containers without managing EC2 servers. |
| Public entry point | Application Load Balancer | Receives internet traffic and forwards it to healthy ECS tasks. |
| Database | Neon Postgres | Stores links, aliases, expiration dates, and click counts. This is outside AWS. |
| Cache/counter | External Redis or ElastiCache Redis | Redis stores hot redirect lookups and generates numeric IDs. External Redis is cheaper; ElastiCache is more production-like. |
| Analytics queue | Amazon SQS | Holds click events so redirect requests do not wait for analytics writes. |
| Analytics worker | ECS Fargate worker service | A separate container consumes SQS events and updates Neon. |
| Networking | VPC, subnets, security groups | Defines where the app runs and which traffic is allowed. |
| Logs | CloudWatch Logs | Stores container logs for debugging. |
| Infrastructure as code | Terraform | Defines AWS resources in code so the setup is repeatable. |

## Request Flow

### Create a Short Link

1. User opens the web app through the Application Load Balancer.
2. ALB forwards the request to the ASP.NET Core container running in ECS.
3. The app validates the long URL and optional custom alias.
4. For generated links, the app gets the next counter value from Redis and encodes it as Base62.
5. The app writes the link record to Neon Postgres.
6. The app stores the active redirect mapping in Redis.
7. The app returns the short URL to the browser.

### Redirect a Short Link

1. User visits `https://short-domain.com/{code}`.
2. The ALB forwards the request to a healthy ECS task.
3. The ASP.NET Core app checks Redis for `code -> longUrl`.
4. If Redis misses, the app reads Neon Postgres.
5. If the link is missing, return `404 Not Found`.
6. If the link is expired, return `410 Gone`.
7. If valid, the app sends a click event to SQS.
8. The app returns `302 Found` to the original long URL.
9. The ECS worker service consumes SQS events and updates analytics in Neon.

## Why This Costs More

- ECS tasks usually run all the time.
- Application Load Balancer has hourly cost.
- If tasks run in private subnets and need internet access, a NAT Gateway can add significant steady cost.
- ElastiCache Redis is production-friendly but not free-tier friendly.
- More CloudWatch logs and metrics may be produced by always-running services.

## Cost-Control Version

For a lower-cost ECS demo:

- Run only one small Fargate web task.
- Use public subnets for the demo task to avoid NAT Gateway.
- Use an external Redis provider instead of ElastiCache.
- Use Neon instead of RDS.
- Keep the worker as a scheduled/manual task or very small always-on service.
- Set CloudWatch log retention to 7 days.

This reduces cost but is less production-like than private subnets plus NAT plus ElastiCache.

## Production-Like Version

For a more realistic production architecture:

- Run ECS tasks in private subnets.
- Put only the ALB in public subnets.
- Use NAT Gateway for outbound access.
- Use ElastiCache Redis inside the VPC.
- Use Secrets Manager for database and Redis credentials.
- Run at least two web tasks across different availability zones.
- Add autoscaling based on CPU, memory, or request count.

This is better engineering practice, but it can exceed free-tier expectations quickly.

## Pros

- Feels like a normal always-running ASP.NET Core web app.
- Better fit for Blazor Web App, Razor Pages, MVC, or server-rendered UI.
- No Lambda cold starts.
- Easier mental model for developers used to web servers.
- Good for learning Docker, ECS, load balancers, health checks, and autoscaling.

## Cons

- More expensive at idle than Lambda.
- More AWS networking to understand.
- ALB, NAT Gateway, and ElastiCache can create steady monthly costs.
- More infrastructure to configure and monitor.
- Scaling is less automatic than Lambda; ECS must add/remove tasks.

## Best Fit

Use this plan when the main goals are:

- Learn container-based AWS deployments.
- Build something closer to a traditional production web app.
- Use Blazor Web App or full ASP.NET Core hosting.
- Avoid Lambda-specific constraints.

## Not a Good Fit When

- You need near-zero idle cost.
- You are trying to stay strictly within AWS free-tier limits.
- You want the fewest possible AWS resources.
- You do not want to manage VPC, subnets, security groups, target groups, and task definitions.

## Comparison With AWS Lambda

| Topic | ECS Fargate Plan | Lambda Plan |
| --- | --- | --- |
| Idle cost | Higher | Very low |
| Compute model | Containers run continuously | Code runs per request/event |
| Public entry point | Application Load Balancer | API Gateway |
| Cold starts | Usually none | Possible |
| Scaling | Add/remove ECS tasks | Automatic per invocation |
| Frontend fit | Blazor Web App or ASP.NET Core | Blazor WebAssembly static hosting |
| Operational complexity | Medium | Lower |
| Free-tier friendliness | Weak to moderate | Strong |
| Best use case | Production-like container learning | Low-cost serverless demo |

## Suggested V1 Terraform Resources

- ECR repository.
- VPC, public subnets, route table, and security groups.
- Application Load Balancer, listener, and target group.
- ECS cluster.
- ECS Fargate service for the web app.
- ECS Fargate service or scheduled task for the analytics worker.
- SQS queue and dead-letter queue.
- IAM task execution role and task role.
- CloudWatch log groups with 7-day retention.
- Optional Secrets Manager entries.

## V1 Application Scope

- Create short links.
- Redirect short links.
- Support custom aliases.
- Support expiration dates.
- Track click counts.
- Show recent links and stats in a dashboard.
- Exclude accounts, billing, abuse detection, custom user domains, and multi-region deployment.
