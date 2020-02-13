#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app
RUN \
  mkdir -p /usr/share/man/man1 && \
  mkdir -p /etc/ssl/certs/java && \
  echo "deb [check-valid-until=no] http://cdn-fastly.deb.debian.org/debian jessie main" > /etc/apt/sources.list.d/jessie.list && \
  echo "deb [check-valid-until=no] http://archive.debian.org/debian jessie-backports main" > /etc/apt/sources.list.d/jessie-backports.list && \
  apt-get -o Acquire::Check-Valid-Until=false update && \
  DEBIAN_FRONTEND=noninteractive \
    apt-get -t jessie-backports -y install openjdk-8-jre-headless \
  && \
  apt-get clean && \
  apt-get autoremove && \
  rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["MinecraftRunnerCore/MinecraftRunnerCore.csproj", "MinecraftRunnerCore/"]
RUN dotnet restore "MinecraftRunnerCore/MinecraftRunnerCore.csproj"
COPY . .
WORKDIR "/src/MinecraftRunnerCore"
RUN dotnet build "MinecraftRunnerCore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MinecraftRunnerCore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 25565
ENTRYPOINT ["dotnet", "MinecraftRunnerCore.dll"]