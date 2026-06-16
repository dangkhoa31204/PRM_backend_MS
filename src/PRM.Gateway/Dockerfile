FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PRM.Gateway/PRM.Gateway.csproj", "src/PRM.Gateway/"]
RUN dotnet restore "src/PRM.Gateway/PRM.Gateway.csproj"
COPY . .
WORKDIR "/src/src/PRM.Gateway"
RUN dotnet build "PRM.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PRM.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PRM.Gateway.dll"]
