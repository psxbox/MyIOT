# Blazor WASM Integration Guide

Руководство по интеграции будущего Blazor WebAssembly фронтенда с `MyIOT.Shared` библиотекой.

## Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                      Browser (WASM)                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          MyIOT.Client (Blazor WASM)                  │  │
│  │  ┌────────────────────────────────────────────────┐  │  │
│  │  │        MyIOT.Shared                            │  │  │
│  │  │  • DTO Models                                  │  │  │
│  │  │  • ApiRoutes Constants                         │  │  │
│  │  │  • MqttTopics Constants                        │  │  │
│  │  │  • Enums (AttributeScope, etc.)                │  │  │
│  │  └────────────────────────────────────────────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              ↕ HTTP/HTTPS
┌─────────────────────────────────────────────────────────────┐
│                      Server (Linux/Docker)                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          MyIOT.Api (ASP.NET Core)                    │  │
│  │  ┌────────────────────────────────────────────────┐  │  │
│  │  │        MyIOT.Shared                            │  │  │
│  │  │  (Same library instance)                       │  │  │
│  │  └────────────────────────────────────────────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- **Zero duplication** — DTO, константы, enum'ы определены один раз
- **Type safety** — клиент и сервер используют одни и те же типы
- **API contracts** — изменения в `ApiRoutes` сразу видны обеим сторонам
- **Refactoring friendly** — переименование полей DTO сразу требует обновления на клиенте

---

## Создание Blazor WASM проекта

### 1. Создать проект

```bash
cd src
dotnet new blazorwasm -n MyIOT.Client -o MyIOT.Client

# Добавить в solution
cd ..
dotnet sln add src/MyIOT.Client/MyIOT.Client.csproj
```

### 2. Добавить ссылку на Shared

```xml
<!-- src/MyIOT.Client/MyIOT.Client.csproj -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="9.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="9.0.2" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- ⭐ Ссылка на Shared библиотеку -->
    <ProjectReference Include="..\MyIOT.Shared\MyIOT.Shared.csproj" />
  </ItemGroup>

</Project>
```

---

## HTTP Client Configuration

### 1. Регистрация HttpClient

```csharp
// src/MyIOT.Client/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyIOT.Client;
using MyIOT.Shared.Constants;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ⭐ Настройка HttpClient с базовым URL API
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("http://localhost:5000") 
});

// Регистрация API-сервисов
builder.Services.AddScoped<IDeviceApiService, DeviceApiService>();
builder.Services.AddScoped<ITelemetryApiService, TelemetryApiService>();
builder.Services.AddScoped<IAttributeApiService, AttributeApiService>();

// Локальное хранилище для JWT-токена
builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();
```

### 2. Сервис для работы с API

```csharp
// src/MyIOT.Client/Services/IDeviceApiService.cs
using MyIOT.Shared.Requests;
using MyIOT.Shared.Responses;
using MyIOT.Shared.Models;

public interface IDeviceApiService
{
    Task<DeviceCreateResponse> CreateDeviceAsync(DeviceCreateRequest request);
    Task<DeviceLoginResponse?> LoginAsync(DeviceLoginRequest request);
    Task<List<DeviceDto>> GetAllDevicesAsync();
    Task<DeviceDto?> GetDeviceByIdAsync(Guid id);
}
```

```csharp
// src/MyIOT.Client/Services/DeviceApiService.cs
using System.Net.Http.Json;
using MyIOT.Shared.Constants;
using MyIOT.Shared.Requests;
using MyIOT.Shared.Responses;
using MyIOT.Shared.Models;

public class DeviceApiService : IDeviceApiService
{
    private readonly HttpClient _http;

    public DeviceApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DeviceCreateResponse> CreateDeviceAsync(DeviceCreateRequest request)
    {
        // ⭐ Используем константу из Shared
        var response = await _http.PostAsJsonAsync(ApiRoutes.Devices.Create, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceCreateResponse>() 
            ?? throw new Exception("Failed to create device");
    }

    public async Task<DeviceLoginResponse?> LoginAsync(DeviceLoginRequest request)
    {
        // ⭐ Используем константу из Shared
        var response = await _http.PostAsJsonAsync(ApiRoutes.Auth.Login, request);
        
        if (!response.IsSuccessStatusCode)
            return null;
            
        return await response.Content.ReadFromJsonAsync<DeviceLoginResponse>();
    }

    public async Task<List<DeviceDto>> GetAllDevicesAsync()
    {
        // ⭐ Используем константу из Shared
        return await _http.GetFromJsonAsync<List<DeviceDto>>(ApiRoutes.Devices.List)
            ?? new List<DeviceDto>();
    }

    public async Task<DeviceDto?> GetDeviceByIdAsync(Guid id)
    {
        // ⭐ Интерполяция маршрута из константы
        var route = ApiRoutes.Devices.GetById.Replace("{id}", id.ToString());
        return await _http.GetFromJsonAsync<DeviceDto>(route);
    }
}
```

---

## JWT Authentication

### 1. AuthStateProvider

```csharp
// src/MyIOT.Client/Auth/CustomAuthStateProvider.cs
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Blazored.LocalStorage;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;

    public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("jwt_token");

        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        // Установить токен в заголовок Authorization
        _http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Парсинг JWT
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    public async Task LoginAsync(string token)
    {
        await _localStorage.SetItemAsync("jwt_token", token);
        
        _http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("jwt_token");
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
```

### 2. Регистрация в Program.cs

```csharp
// Добавить в builder.Services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>();
```

---

## Компоненты UI

### 1. Страница логина

```razor
@* src/MyIOT.Client/Pages/Login.razor *@
@page "/login"
@inject IDeviceApiService DeviceApi
@inject CustomAuthStateProvider AuthProvider
@inject NavigationManager Navigation

<h3>Device Login</h3>

<EditForm Model="@loginRequest" OnValidSubmit="HandleLogin">
    <div class="mb-3">
        <label>Access Token</label>
        <InputText class="form-control" @bind-Value="loginRequest.AccessToken" />
    </div>
    
    @if (!string.IsNullOrEmpty(errorMessage))
    {
        <div class="alert alert-danger">@errorMessage</div>
    }
    
    <button type="submit" class="btn btn-primary" disabled="@isLoading">
        @if (isLoading)
        {
            <span class="spinner-border spinner-border-sm"></span>
        }
        Login
    </button>
</EditForm>

@code {
    // ⭐ Используем тип из Shared
    private DeviceLoginRequest loginRequest = new();
    private string errorMessage = string.Empty;
    private bool isLoading = false;

    private async Task HandleLogin()
    {
        isLoading = true;
        errorMessage = string.Empty;

        try
        {
            // ⭐ Используем Shared Response
            var response = await DeviceApi.LoginAsync(loginRequest);
            
            if (response is not null)
            {
                await AuthProvider.LoginAsync(response.Token);
                Navigation.NavigateTo("/");
            }
            else
            {
                errorMessage = "Invalid access token";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }
}
```

### 2. Dashboard с телеметрией

```razor
@* src/MyIOT.Client/Pages/Dashboard.razor *@
@page "/dashboard/{DeviceId:guid}"
@inject ITelemetryApiService TelemetryApi
@attribute [Authorize]

<h3>Device Telemetry Dashboard</h3>

@if (latestData is null)
{
    <p><em>Loading...</em></p>
}
else if (latestData.Count == 0)
{
    <p><em>No telemetry data available.</em></p>
}
else
{
    <div class="row">
        @foreach (var item in latestData)
        {
            <div class="col-md-3">
                <div class="card mb-3">
                    <div class="card-body">
                        <h5 class="card-title">@item.Key</h5>
                        <p class="display-6">@item.Value.ToString("F2")</p>
                        <small class="text-muted">
                            @item.Timestamp.ToString("HH:mm:ss")
                        </small>
                    </div>
                </div>
            </div>
        }
    </div>
}

<button class="btn btn-primary" @onclick="RefreshData">
    <i class="bi bi-arrow-clockwise"></i> Refresh
</button>

@code {
    [Parameter]
    public Guid DeviceId { get; set; }

    // ⭐ Используем тип из Shared
    private List<TelemetryLatestResponse>? latestData;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        latestData = await TelemetryApi.GetLatestAsync(DeviceId);
    }

    private async Task RefreshData()
    {
        await LoadData();
    }
}
```

---

## Real-time Updates (SignalR)

### 1. Добавить SignalR на сервере

```csharp
// src/MyIOT.Api/Hubs/TelemetryHub.cs
using Microsoft.AspNetCore.SignalR;

public class TelemetryHub : Hub
{
    public async Task SubscribeToDevice(string deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
    }

    public async Task UnsubscribeFromDevice(string deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device_{deviceId}");
    }
}
```

```csharp
// src/MyIOT.Api/Program.cs
// Добавить перед app.Build()
builder.Services.AddSignalR();

// Добавить после app.UseAuthorization()
app.MapHub<TelemetryHub>("/hubs/telemetry");
```

### 2. Модифицировать TelemetryService для отправки уведомлений

```csharp
// src/MyIOT.Api/Services/TelemetryService.cs
// Добавить IHubContext<TelemetryHub> в конструктор

public async Task SaveAsync(Guid deviceId, Dictionary<string, double> values)
{
    // ... существующий код ...

    // ⭐ Отправка real-time уведомления
    await _hubContext.Clients.Group($"device_{deviceId}")
        .SendAsync("TelemetryUpdated", records);
}
```

### 3. SignalR на клиенте

```csharp
// src/MyIOT.Client/Services/TelemetryRealtimeService.cs
using Microsoft.AspNetCore.SignalR.Client;
using MyIOT.Shared.Responses;

public class TelemetryRealtimeService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    
    public event Action<List<TelemetryLatestResponse>>? OnTelemetryUpdated;

    public async Task ConnectAsync(string hubUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<List<TelemetryLatestResponse>>("TelemetryUpdated", data =>
        {
            OnTelemetryUpdated?.Invoke(data);
        });

        await _hubConnection.StartAsync();
    }

    public async Task SubscribeToDeviceAsync(Guid deviceId)
    {
        if (_hubConnection is not null)
            await _hubConnection.InvokeAsync("SubscribeToDevice", deviceId.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
```

---

## CORS Configuration (важно!)

### На сервере (MyIOT.Api/Program.cs)

```csharp
// ⭐ Обновить политику CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(
            "https://localhost:5002",  // Blazor WASM dev server
            "http://localhost:5003"    // Альтернативный порт
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); // Для SignalR
    });
});
```

---

## Deployment

### Production Build

```bash
# Сборка Blazor WASM
cd src/MyIOT.Client
dotnet publish -c Release

# Результат в: bin/Release/net9.0/publish/wwwroot
```

### Hosting Options

**Option 1: ASP.NET Core Host (рекомендуется)**
```csharp
// Добавить в MyIOT.Api/Program.cs
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapFallbackToFile("index.html");
```

**Option 2: Nginx**
```nginx
server {
    listen 80;
    server_name myiot.example.com;

    root /var/www/myiot/wwwroot;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

---

## Best Practices

### 1. Версионирование API

```csharp
// MyIOT.Shared/Constants/ApiRoutes.cs
public static class ApiRoutes
{
    private const string Base = "/api/v1"; // ⭐ Версионирование
    
    public static class Devices
    {
        public const string Create = $"{Base}/devices";
        // ...
    }
}
```

### 2. Error Handling

```csharp
// MyIOT.Client/Services/ApiException.cs
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? Response { get; }

    public ApiException(int statusCode, string? response) 
        : base($"API call failed with status {statusCode}")
    {
        StatusCode = statusCode;
        Response = response;
    }
}
```

### 3. Loading States

```csharp
// MyIOT.Client/Components/LoadingSpinner.razor
<div class="spinner-border text-primary" role="status">
    <span class="visually-hidden">Loading...</span>
</div>
```

---

## Security Checklist

- [ ] HTTPS в production
- [ ] Короткий срок действия JWT (проверять `expiresAt`)
- [ ] Refresh token для автоматического обновления JWT
- [ ] XSS protection (Blazor автоматически экранирует)
- [ ] CSP headers
- [ ] Rate limiting на API
- [ ] Input validation (DataAnnotations на DTO)

---

## Пример: Полный цикл регистрации и отправки телеметрии

```razor
@page "/quick-start"
@inject IDeviceApiService DeviceApi
@inject ITelemetryApiService TelemetryApi
@inject CustomAuthStateProvider AuthProvider

<h3>Quick Start Demo</h3>

<button @onclick="RunDemo" disabled="@isRunning">Run Complete Demo</button>

<pre>@log</pre>

@code {
    private bool isRunning = false;
    private string log = "";

    private async Task RunDemo()
    {
        isRunning = true;
        log = "";

        try
        {
            // 1. Создать устройство
            Log("Creating device...");
            var createReq = new DeviceCreateRequest { Name = "DemoDevice" };
            var device = await DeviceApi.CreateDeviceAsync(createReq);
            Log($"✓ Device created: {device.Id}");
            Log($"  Access Token: {device.AccessToken}");

            // 2. Аутентификация
            Log("\nAuthenticating...");
            var loginReq = new DeviceLoginRequest { AccessToken = device.AccessToken };
            var loginResp = await DeviceApi.LoginAsync(loginReq);
            
            if (loginResp is null)
            {
                Log("✗ Authentication failed");
                return;
            }

            await AuthProvider.LoginAsync(loginResp.Token);
            Log($"✓ JWT obtained (expires: {loginResp.ExpiresAt})");

            // 3. Отправить телеметрию
            Log("\nSending telemetry...");
            var telemetryReq = new TelemetryRequest
            {
                Values = new()
                {
                    ["temperature"] = 25.5,
                    ["humidity"] = 60.0
                }
            };
            await TelemetryApi.SendAsync(telemetryReq);
            Log("✓ Telemetry sent");

            // 4. Получить последние значения
            await Task.Delay(500);
            Log("\nFetching latest telemetry...");
            var latest = await TelemetryApi.GetLatestAsync(device.Id);
            
            foreach (var item in latest)
            {
                Log($"  {item.Key}: {item.Value} ({item.Timestamp:HH:mm:ss})");
            }

            Log("\n✅ Demo completed successfully!");
        }
        catch (Exception ex)
        {
            Log($"\n❌ Error: {ex.Message}");
        }
        finally
        {
            isRunning = false;
        }
    }

    private void Log(string message)
    {
        log += message + "\n";
        StateHasChanged();
    }
}
```

---

## Дальнейшие улучшения

- [ ] **Offline support** — IndexedDB для кэширования
- [ ] **PWA** — Service Worker для работы без интернета
- [ ] **Charts** — ChartJS / Blazor.Charts для визуализации
- [ ] **Dark theme** — MudBlazor / Radzen UI components
- [ ] **Mobile app** — .NET MAUI Blazor Hybrid
