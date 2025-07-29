FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["HttpCache/HttpCache.csproj", "HttpCache/"]
COPY ["HttpCache.Data/HttpCache.Data.csproj", "HttpCache.Data/"]
RUN dotnet restore "HttpCache/HttpCache.csproj"
RUN dotnet restore "HttpCache.Data/HttpCache.Data.csproj"
COPY . .
WORKDIR "/src/HttpCache"
RUN dotnet build "HttpCache.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HttpCache.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HttpCache.dll"]
