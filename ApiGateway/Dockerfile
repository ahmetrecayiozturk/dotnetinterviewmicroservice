FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# common projeyi kopyala
COPY ["common/common.csproj", "common/"]
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]

# Restore
RUN dotnet restore "ApiGateway/ApiGateway.csproj"

# Tüm solution içeriğini kopyala
COPY . .

# Build
WORKDIR "/src/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]