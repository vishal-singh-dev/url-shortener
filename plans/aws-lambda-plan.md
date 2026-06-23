# Bitly Clone Infrastructure Plan: AWS Lambda

## What This Plan Is

This version is built for a cost-sensitive portfolio demo. It uses serverless AWS services so the app has little to no idle cost. Code runs when someone opens the app, creates a short link, redirects through a short URL, or when an analytics event needs processing.

## Infrastructure Overview

| Part | Service | Beginner explanation |
| --- | --- | --- |
| Frontend hosting | S3 static website, optional CloudFront | Stores the Blazor WebAssembly files and serves them to the browser. CloudFront can make it faster and support a custom domain. |
| HTTP entry point | API Gateway HTTP API | Receives browser/API requests and forwards them to Lambda. Think of it as the public door for the backend. |
| API compute | AWS Lambda with .NET | Runs backend code only when a request happens. No server stays on all day. |
| Database | Neon Postgres | Stores links, aliases, expiration dates, and click counts. This is outside AWS. |
| Cache/counter | External Redis | Stores hot redirect lookups and generates numeric IDs for Base62 short codes. |
| Analytics queue | Amazon SQS | Holds click events so redirect requests do not wait for analytics writes. |
| Analytics worker | SQS-triggered Lambda | Processes queued click events and updates Neon. |
| Logs | CloudWatch Logs | Stores Lambda logs for debugging. Keep retention short to control cost. |
| Infrastructure as code | Terraform | Defines AWS resources in code so the setup is repeatable. |

## Request Flow

### Create a Short Link

1. User opens the Blazor app from S3 or CloudFront.
2. Browser sends `POST /api/urls` to API Gateway.
3. API Gateway invokes the .NET Lambda function.
4. Lambda validates the long URL and optional custom alias.
5. For generated links, Lambda gets the next counter value from Redis and encodes it as Base62.
6. Lambda writes the link record to Neon Postgres.
7. Lambda stores the active redirect mapping in Redis.
8. Lambda returns the short URL to the browser.

### Redirect a Short Link

1. User visits `https://short-domain.com/{code}`.
2. API Gateway invokes the redirect Lambda.
3. Lambda checks Redis for `code -> longUrl`.
4. If Redis misses, Lambda reads Neon Postgres.
5. If the link is missing, return `404 Not Found`.
6. If the link is expired, return `410 Gone`.
7. If valid, Lambda sends a click event to SQS.
8. Lambda returns `302 Found` to the original long URL.
9. A separate Lambda later processes the SQS event and updates analytics in Neon.

## Why This Is Cheap

- Lambda has no always-running server.
- API Gateway charges by request.
- SQS charges by usage.
- S3 static hosting is very cheap for small traffic.
- No ECS tasks, Application Load Balancer, NAT Gateway, RDS, or ElastiCache are required.

## Important Cost Notes

- Keep Lambdas outside a VPC. If Lambda is placed in private subnets, it may need a NAT Gateway to reach Neon/Redis, and NAT Gateway is not free-tier friendly.
- Use encrypted Lambda environment variables instead of Secrets Manager for a simple demo, because Secrets Manager has monthly secret charges.
- Set CloudWatch log retention to 7 days.
- CloudFront is optional. It is useful for custom domains and caching, but not required for the simplest demo.

## Pros

- Best fit for AWS free-tier or near-zero idle cost.
- Easy to scale from zero to moderate traffic.
- Good for demonstrating serverless design.
- No container orchestration to manage.
- SQS makes analytics resilient without slowing redirects.

## Cons

- Cold starts can make the first request slower.
- Lambda is less natural for always-on server apps.
- Database and Redis connections must be handled carefully because Lambdas can scale out quickly.
- Blazor Server is not a good fit here; use Blazor WebAssembly instead.
- Local development feels different from production because production is event-driven.

## Best Fit

Use this plan when the main goals are:

- Keep AWS cost very low.
- Build a portfolio-friendly project.
- Learn serverless AWS.
- Avoid running infrastructure 24/7.

## Not a Good Fit When

- You want an always-running ASP.NET Core web server.
- You want Blazor Server with persistent SignalR connections.
- You expect heavy traffic and need very predictable low latency.
- You want to learn containers, load balancers, and ECS specifically.

## Comparison With ECS Fargate

| Topic | Lambda Plan | ECS Fargate Plan |
| --- | --- | --- |
| Idle cost | Very low | Higher because tasks and load balancer stay running |
| Compute model | Runs per request/event | Containers run continuously |
| Public entry point | API Gateway | Application Load Balancer |
| Cold starts | Possible | Usually no cold starts |
| Scaling | Automatic per invocation | Scales by running more tasks |
| Frontend fit | Blazor WebAssembly static hosting | Blazor Web App or full ASP.NET Core hosting |
| Operational complexity | Lower | Medium |
| Free-tier friendliness | Strong | Weak to moderate |
| Best use case | Low-cost demo/serverless learning | Production-like container learning |

## Suggested V1 Terraform Resources

- S3 bucket for Blazor WebAssembly static files.
- API Gateway HTTP API.
- Lambda function for HTTP API.
- Lambda function for SQS analytics worker.
- SQS queue and dead-letter queue.
- IAM roles and policies for Lambda.
- CloudWatch log groups with 7-day retention.
- Optional CloudFront distribution.

## V1 Application Scope

- Create short links.
- Redirect short links.
- Support custom aliases.
- Support expiration dates.
- Track click counts.
- Show recent links and stats in a dashboard.
- Exclude accounts, billing, abuse detection, custom user domains, and multi-region deployment.
