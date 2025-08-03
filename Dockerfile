# 构建阶段
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 复制项目文件（利用缓存）
COPY ["LeSi-AI-Api.sln", "./"]
COPY ["LeSi.AI.WebApi/LeSi.AI.WebApi.csproj", "LeSi.AI.WebApi/"]
COPY ["LeSi.AI.Infrastructure/LeSi.AI.Infrastructure.csproj", "LeSi.AI.Infrastructure/"]

# 还原依赖
RUN dotnet restore "LeSi-AI-Api.sln"

# 复制源码并构建
COPY . .
RUN dotnet publish "LeSi.AI.WebApi/LeSi.AI.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# 设置非root用户
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser /app
USER appuser

# 复制构建结果
COPY --from=build /app/publish .

# 暴露应用端口
EXPOSE 5027

ENTRYPOINT ["dotnet", "LeSi.AI.WebApi.dll"]