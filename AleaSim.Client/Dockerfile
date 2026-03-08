FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AleaSim.Client/AleaSim.Client.csproj", "AleaSim.Client/"]
COPY ["AleaSim.Shared/AleaSim.Shared.csproj", "AleaSim.Shared/"]
RUN dotnet restore "AleaSim.Client/AleaSim.Client.csproj"
COPY . .
WORKDIR "/src/AleaSim.Client"
RUN dotnet publish "AleaSim.Client.csproj" -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .

RUN echo "server { \
    listen 80; \
    location / { \
        root /usr/share/nginx/html; \
        try_files \$uri \$uri/ /index.html =404; \
    } \
}" > /etc/nginx/conf.d/default.conf

EXPOSE 80
