# ==================================================
# Build stage
# ==================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy backend source.
COPY src/ ./src/

# Restore only the API project and everything it
# references through ProjectReference.
RUN dotnet restore \
    src/CartCompare.Api/CartCompare.Api.csproj

# Publish the production API.
RUN dotnet publish \
    src/CartCompare.Api/CartCompare.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore


# ==================================================
# Runtime stage
# ==================================================

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT [
    "dotnet",
    "CartCompare.Api.dll"
]