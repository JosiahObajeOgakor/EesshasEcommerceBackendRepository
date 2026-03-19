param(
    [Parameter(Mandatory = $true)] [string]$AccountId,
    [Parameter(Mandatory = $false)] [string]$Region = "us-east-1",
    [Parameter(Mandatory = $false)] [string]$EcrRepository = "easshas-webapi",
    [Parameter(Mandatory = $false)] [string]$ClusterName = "easshas-cluster",
    [Parameter(Mandatory = $false)] [string]$ServiceName = "easshas-api",
    [Parameter(Mandatory = $true)] [string]$TaskExecutionRoleArn,
    [Parameter(Mandatory = $false)] [string]$TaskRoleArn = "",
    [Parameter(Mandatory = $true)] [string]$Subnets,
    [Parameter(Mandatory = $true)] [string]$SecurityGroups,
    [Parameter(Mandatory = $false)] [int]$DesiredCount = 1,
    [Parameter(Mandatory = $false)] [string]$EnvironmentFile = ".env",
    [Parameter(Mandatory = $false)] [switch]$AssignPublicIp
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "[deploy] $Message" -ForegroundColor Cyan
}

function Require-Command([string]$CommandName) {
    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$CommandName' was not found in PATH."
    }
}

function Parse-DotEnv([string]$Path) {
    if (-not (Test-Path $Path)) {
        throw "Environment file '$Path' not found."
    }

    $map = @{}
    foreach ($line in Get-Content $Path) {
        $trim = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trim)) { continue }
        if ($trim.StartsWith("#")) { continue }
        $idx = $trim.IndexOf("=")
        if ($idx -lt 1) { continue }

        $key = $trim.Substring(0, $idx).Trim()
        $value = $trim.Substring($idx + 1).Trim()

        if ($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $map[$key] = $value
    }
    return $map
}

function Require-Key($Map, [string]$Key) {
    if (-not $Map.ContainsKey($Key) -or [string]::IsNullOrWhiteSpace($Map[$Key])) {
        throw "Missing required key '$Key' in .env file."
    }
}

Require-Command "aws"
Require-Command "docker"
Require-Command "git"

Write-Step "Loading environment values from $EnvironmentFile"
$envMap = Parse-DotEnv -Path $EnvironmentFile

$required = @(
    "CONNECTIONSTRINGS_POSTGRES",
    "JWT_KEY",
    "JWT_ISSUER",
    "JWT_AUDIENCE",
    "PAYSTACK_SECRET_KEY",
    "OPENAI_API_KEY",
    "EMAIL_FROM",
    "EMAIL_ADMIN",
    "AWS_REGION"
)

foreach ($k in $required) {
    Require-Key $envMap $k
}

$repoUri = "$AccountId.dkr.ecr.$Region.amazonaws.com/$EcrRepository"
$gitSha = (git rev-parse --short HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($gitSha)) {
    $gitSha = (Get-Date -Format "yyyyMMddHHmmss")
}
$imageTag = $gitSha
$imageUri = "$repoUri`:$imageTag"

Write-Step "Ensuring ECR repository exists: $EcrRepository"
$repoCheck = aws ecr describe-repositories --repository-names $EcrRepository --region $Region 2>$null
if ($LASTEXITCODE -ne 0) {
    aws ecr create-repository --repository-name $EcrRepository --region $Region | Out-Null
}

Write-Step "Logging in to ECR"
$loginPassword = aws ecr get-login-password --region $Region
$loginPassword | docker login --username AWS --password-stdin "$AccountId.dkr.ecr.$Region.amazonaws.com" | Out-Null

Write-Step "Building Docker image: $imageUri"
docker build -f src/Easshas.WebApi/Dockerfile -t $imageUri .

Write-Step "Pushing Docker image"
docker push $imageUri

$subnetArray = $Subnets.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$sgArray = $SecurityGroups.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if ($subnetArray.Count -eq 0) { throw "No subnets provided." }
if ($sgArray.Count -eq 0) { throw "No security groups provided." }

$containerName = "easshas-api"

$containerEnv = @(
    @{ name = "ASPNETCORE_ENVIRONMENT"; value = "Production" },
    @{ name = "ConnectionStrings__Postgres"; value = $envMap["CONNECTIONSTRINGS_POSTGRES"] },
    @{ name = "Jwt__Key"; value = $envMap["JWT_KEY"] },
    @{ name = "Jwt__Issuer"; value = $envMap["JWT_ISSUER"] },
    @{ name = "Jwt__Audience"; value = $envMap["JWT_AUDIENCE"] },
    @{ name = "Paystack__SecretKey"; value = $envMap["PAYSTACK_SECRET_KEY"] },
    @{ name = "App__AI__ApiKey"; value = $envMap["OPENAI_API_KEY"] },
    @{ name = "Email__From"; value = $envMap["EMAIL_FROM"] },
    @{ name = "Email__Admin"; value = $envMap["EMAIL_ADMIN"] },
    @{ name = "AWS__Region"; value = $envMap["AWS_REGION"] }
)

$taskDef = @{
    family = "$ServiceName-task"
    networkMode = "awsvpc"
    requiresCompatibilities = @("FARGATE")
    cpu = "512"
    memory = "1024"
    executionRoleArn = $TaskExecutionRoleArn
    containerDefinitions = @(
        @{
            name = $containerName
            image = $imageUri
            essential = $true
            portMappings = @(
                @{
                    containerPort = 8080
                    hostPort = 8080
                    protocol = "tcp"
                }
            )
            environment = $containerEnv
            logConfiguration = @{
                logDriver = "awslogs"
                options = @{
                    "awslogs-group" = "/ecs/$ServiceName"
                    "awslogs-region" = $Region
                    "awslogs-stream-prefix" = "ecs"
                }
            }
        }
    )
}

if (-not [string]::IsNullOrWhiteSpace($TaskRoleArn)) {
    $taskDef["taskRoleArn"] = $TaskRoleArn
}

Write-Step "Ensuring CloudWatch log group exists"
aws logs create-log-group --log-group-name "/ecs/$ServiceName" --region $Region 2>$null | Out-Null

$tempTaskFile = Join-Path $env:TEMP "ecs-taskdef-$ServiceName.json"
$taskDef | ConvertTo-Json -Depth 20 | Set-Content -Path $tempTaskFile -Encoding UTF8

Write-Step "Registering task definition"
$regJson = aws ecs register-task-definition --cli-input-json "file://$tempTaskFile" --region $Region | ConvertFrom-Json
$taskDefArn = $regJson.taskDefinition.taskDefinitionArn

Write-Step "Ensuring ECS cluster exists"
aws ecs describe-clusters --clusters $ClusterName --region $Region | Out-Null
if ($LASTEXITCODE -ne 0) {
    aws ecs create-cluster --cluster-name $ClusterName --region $Region | Out-Null
}

$assignIpValue = if ($AssignPublicIp.IsPresent) { "ENABLED" } else { "DISABLED" }
$subnetJson = ($subnetArray | ForEach-Object { '"' + $_ + '"' }) -join ","
$sgJson = ($sgArray | ForEach-Object { '"' + $_ + '"' }) -join ","
$networkConfig = "awsvpcConfiguration={subnets=[$subnetJson],securityGroups=[$sgJson],assignPublicIp=$assignIpValue}"

Write-Step "Checking if ECS service exists"
$existing = aws ecs describe-services --cluster $ClusterName --services $ServiceName --region $Region | ConvertFrom-Json
$serviceExists = $false
if ($existing.services -and $existing.services.Count -gt 0) {
    if ($existing.services[0].status -ne "INACTIVE") {
        $serviceExists = $true
    }
}

if ($serviceExists) {
    Write-Step "Updating ECS service"
    aws ecs update-service --cluster $ClusterName --service $ServiceName --task-definition $taskDefArn --desired-count $DesiredCount --region $Region | Out-Null
}
else {
    Write-Step "Creating ECS service"
    aws ecs create-service --cluster $ClusterName --service-name $ServiceName --task-definition $taskDefArn --desired-count $DesiredCount --launch-type FARGATE --network-configuration $networkConfig --region $Region | Out-Null
}

Write-Step "Deployment submitted"
Write-Host "Image: $imageUri"
Write-Host "TaskDefinition: $taskDefArn"
Write-Host "Cluster: $ClusterName"
Write-Host "Service: $ServiceName"
Write-Host "To watch rollout: aws ecs describe-services --cluster $ClusterName --services $ServiceName --region $Region"
