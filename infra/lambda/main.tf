terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

locals {
  name = var.app_name

  common_tags = {
    Project     = var.app_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

variable "app_name" {
  description = "Short name used for AWS resource names."
  type        = string
  default     = "bitly-clone"
}

variable "environment" {
  description = "Deployment environment name."
  type        = string
  default     = "dev"
}

variable "aws_region" {
  description = "AWS region to deploy into."
  type        = string
  default     = "ap-south-1"
}

variable "api_lambda_zip_path" {
  description = "Path to the zipped .NET Lambda package for the HTTP API."
  type        = string
  default     = "../../artifacts/api-lambda.zip"
}

variable "worker_lambda_zip_path" {
  description = "Unused. Kept temporarily for compatibility with old tfvars files."
  type        = string
  default     = ""
}

variable "api_lambda_handler" {
  description = "Handler for the .NET HTTP API Lambda executable assembly."
  type        = string
  default     = "Api"
}

variable "neon_database_url" {
  description = "Neon Postgres pooled connection string."
  type        = string
  sensitive   = true
  default     = ""
}

variable "redis_connection_string" {
  description = "External Redis connection string."
  type        = string
  sensitive   = true
  default     = ""
}

variable "cors_allowed_origins" {
  description = "Allowed frontend origins for browser calls."
  type        = list(string)
  default     = ["*"]
}

resource "aws_s3_bucket" "frontend" {
  bucket_prefix = "${local.name}-${var.environment}-frontend-"
  force_destroy = true

  tags = local.common_tags
}

resource "aws_s3_bucket_website_configuration" "frontend" {
  bucket = aws_s3_bucket.frontend.id

  index_document {
    suffix = "index.html"
  }

  error_document {
    key = "index.html"
  }
}

resource "aws_s3_bucket_public_access_block" "frontend" {
  bucket = aws_s3_bucket.frontend.id

  block_public_acls       = false
  block_public_policy     = false
  ignore_public_acls      = false
  restrict_public_buckets = false
}

resource "aws_s3_bucket_policy" "frontend_read" {
  bucket = aws_s3_bucket.frontend.id

  depends_on = [aws_s3_bucket_public_access_block.frontend]

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "PublicReadForStaticWebsite"
        Effect    = "Allow"
        Principal = "*"
        Action    = "s3:GetObject"
        Resource  = "${aws_s3_bucket.frontend.arn}/*"
      }
    ]
  })
}

resource "aws_iam_role" "api_lambda" {
  name = "${local.name}-${var.environment}-api-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "api_basic_execution" {
  role       = aws_iam_role.api_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_cloudwatch_log_group" "api_lambda" {
  name              = "/aws/lambda/${local.name}-${var.environment}-api"
  retention_in_days = 7

  tags = local.common_tags
}

resource "aws_lambda_function" "api" {
  function_name = "${local.name}-${var.environment}-api"
  role          = aws_iam_role.api_lambda.arn
  runtime       = "dotnet8"
  handler       = var.api_lambda_handler
  architectures = ["arm64"]

  filename         = var.api_lambda_zip_path
  source_code_hash = filebase64sha256(var.api_lambda_zip_path)

  memory_size = 512
  timeout     = 15

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT     = var.environment
      ConnectionStrings__Default = var.neon_database_url
      Redis__ConnectionString    = var.redis_connection_string
      FRONTEND_BUCKET_NAME       = aws_s3_bucket.frontend.bucket
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.api_lambda,
    aws_iam_role_policy_attachment.api_basic_execution
  ]

  tags = local.common_tags
}

resource "aws_apigatewayv2_api" "http" {
  name          = "${local.name}-${var.environment}-http-api"
  protocol_type = "HTTP"

  cors_configuration {
    allow_credentials = false
    allow_headers     = ["content-type", "authorization"]
    allow_methods     = ["GET", "POST", "OPTIONS"]
    allow_origins     = var.cors_allowed_origins
    max_age           = 300
  }

  tags = local.common_tags
}

resource "aws_apigatewayv2_integration" "api_lambda" {
  api_id                 = aws_apigatewayv2_api.http.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.api.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "default" {
  api_id    = aws_apigatewayv2_api.http.id
  route_key = "$default"
  target    = "integrations/${aws_apigatewayv2_integration.api_lambda.id}"
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.http.id
  name        = "$default"
  auto_deploy = true

  tags = local.common_tags
}

resource "aws_lambda_permission" "allow_apigateway" {
  statement_id  = "AllowExecutionFromApiGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.http.execution_arn}/*/*"
}

output "api_base_url" {
  description = "Base URL for API Gateway. The redirect route also uses this host unless you add a custom domain."
  value       = aws_apigatewayv2_api.http.api_endpoint
}

output "frontend_bucket_name" {
  description = "S3 bucket where Blazor WebAssembly files should be uploaded."
  value       = aws_s3_bucket.frontend.bucket
}

output "frontend_website_url" {
  description = "S3 website endpoint for the frontend."
  value       = aws_s3_bucket_website_configuration.frontend.website_endpoint
}
