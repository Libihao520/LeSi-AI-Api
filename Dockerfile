# 构建阶段保持不变
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LeSi-AI-Api.sln", "./"]
COPY ["LeSi.AI.WebApi/LeSi.AI.WebApi.csproj", "LeSi.AI.WebApi/"]
COPY ["LeSi.AI.Infrastructure/LeSi.AI.Infrastructure.csproj", "LeSi.AI.Infrastructure/"]
RUN dotnet restore "LeSi-AI-Api.sln"
COPY . .
RUN dotnet publish "LeSi.AI.WebApi/LeSi.AI.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# 安装微软字体
RUN apt-get update && \
    apt-get install -y --no-install-recommends wget cabextract fontconfig && \
    mkdir -p /usr/share/fonts/truetype/msttcorefonts && \
    cd /usr/share/fonts/truetype/msttcorefonts && \
    wget -q https://www.freedesktop.org/software/fontconfig/webfonts/webfonts.tar.gz && \
    tar -xzf webfonts.tar.gz && \
    cd msfonts && \
    cabextract *.exe && \
    cp *.ttf *.TTF ../ && \
    cd .. && \
    rm -rf msfonts webfonts.tar.gz && \
    apt-get remove -y wget cabextract && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/* && \
    fc-cache -fv

# 设置非root用户
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser /app
USER appuser

# 复制构建结果
COPY --from=build /app/publish .

# 暴露应用端口
EXPOSE 5027

ENTRYPOINT ["dotnet", "LeSi.AI.WebApi.dll"]