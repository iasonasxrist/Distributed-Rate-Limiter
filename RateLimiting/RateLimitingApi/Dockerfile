FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RateLimitingApi/RateLimitingApi.csproj RateLimitingApi/
COPY RateLimiting.Infrastructure/RateLimiting.Infrastructure.csproj RateLimiting.Infrastructure/
COPY RateLimiting.Domain/RateLimiting.Domain.csproj RateLimiting.Domain/
RUN dotnet restore RateLimitingApi/RateLimitingApi.csproj
COPY . .
RUN dotnet publish RateLimitingApi/RateLimitingApi.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet","RateLimitingApi.dll"]
