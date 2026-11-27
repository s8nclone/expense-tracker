# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better Docker layer caching
COPY src/ExpenseTracker.Api/ExpenseTracker.Api.csproj src/ExpenseTracker.Api/
RUN dotnet restore src/ExpenseTracker.Api/ExpenseTracker.Api.csproj

# Copy remaining source and publish
COPY src/ExpenseTracker.Api/ src/ExpenseTracker.Api/
RUN dotnet publish src/ExpenseTracker.Api/ExpenseTracker.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create a writable directory for the SQLite database owned by the non-root app user
RUN mkdir -p /app/data && chown -R app:app /app/data

COPY --from=build /app/publish .

USER app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/expenses.db"
ENV Swagger__Enabled=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
