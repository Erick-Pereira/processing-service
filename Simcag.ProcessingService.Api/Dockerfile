# Build context: raiz do repositório
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["shared/shared.csproj", "shared/"]
COPY ["processing-service/Simcag.ProcessingService.Api/Simcag.ProcessingService.Api.csproj", "processing-service/Simcag.ProcessingService.Api/"]
COPY ["processing-service/Simcag.ProcessingService.Application/Simcag.ProcessingService.Application.csproj", "processing-service/Simcag.ProcessingService.Application/"]
COPY ["processing-service/Simcag.ProcessingService.Domain/Simcag.ProcessingService.Domain.csproj", "processing-service/Simcag.ProcessingService.Domain/"]
COPY ["processing-service/Simcag.ProcessingService.Infrastructure/Simcag.ProcessingService.Infrastructure.csproj", "processing-service/Simcag.ProcessingService.Infrastructure/"]
RUN dotnet restore "processing-service/Simcag.ProcessingService.Api/Simcag.ProcessingService.Api.csproj"
COPY shared/ shared/
COPY processing-service/ processing-service/
RUN dotnet publish "processing-service/Simcag.ProcessingService.Api/Simcag.ProcessingService.Api.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Simcag.ProcessingService.Api.dll"]
