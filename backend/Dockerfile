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

# Npgsql can use GSSAPI/Kerberos support on Linux.
# Install the native library it expects so startup
# does not emit the libgssapi_krb5.so.2 warning.
RUN apt-get update \
    && apt-get install -y \
        --no-install-recommends \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CartCompare.Api.dll"]